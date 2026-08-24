using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRMS.Application.DTOs.Webhook;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Webhook;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;
    private readonly System.Threading.Channels.ChannelWriter<WebhookJob> _dispatcher;
    // Configurable domain allowlist (Webhook:AllowedDomainSuffixes in appsettings).
    // When non-empty, only HTTPS URLs whose hostname ends with one of these suffixes
    // are accepted — provides an explicit allowlist on top of the private-IP blocklist.
    private readonly IReadOnlyList<string> _allowedDomainSuffixes;

    public WebhookService(ApplicationDbContext db, IHttpClientFactory httpClientFactory,
                          ILogger<WebhookService> logger,
                          System.Threading.Channels.ChannelWriter<WebhookJob> dispatcher,
                          IConfiguration configuration)
    {
        _db = db; _httpClientFactory = httpClientFactory;
        _logger = logger; _dispatcher = dispatcher;

        // Read comma-separated or JSON-array config — both formats are supported.
        // docker-compose passes WEBHOOK_ALLOWED_DOMAIN_SUFFIXES as a comma-separated string.
        var raw = configuration["Webhook:AllowedDomainSuffixes"] ?? string.Empty;
        _allowedDomainSuffixes = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToList()
            .AsReadOnly();
    }

    public async Task<List<WebhookDto>> ListAsync(int? companyId)
    {
        var list = await _db.WebhookSubscriptions
            .Where(w => w.IsActive && (companyId == null || w.CompanyId == companyId))
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<WebhookDto> RegisterAsync(int? companyId, CreateWebhookDto dto)
    {
        // SSRF guard 1: reject private/loopback IP ranges.
        if (!IsAllowedWebhookUrl(dto.TargetUrl))
            throw new ArgumentException(
                "Webhook target URL must be a public HTTPS address. " +
                "Private IP ranges, loopback, and cloud metadata endpoints are not allowed.");

        // SSRF guard 2 (allowlist): when Webhook:AllowedDomainSuffixes is non-empty in
        // appsettings, only URLs whose host ends with a listed suffix are accepted.
        // This narrows the SSRF surface from "any public IP" to "known trusted domains".
        if (_allowedDomainSuffixes.Count > 0 &&
            Uri.TryCreate(dto.TargetUrl, UriKind.Absolute, out var parsedUri))
        {
            var host = parsedUri.Host.ToLowerInvariant();
            var allowed = _allowedDomainSuffixes.Any(suffix =>
                host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal));
            if (!allowed)
                throw new ArgumentException(
                    $"Webhook target host '{parsedUri.Host}' is not in the configured allowlist. " +
                    "Contact your administrator to add it to Webhook:AllowedDomainSuffixes.");
        }

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sub = new WebhookSubscription
        {
            CompanyId = companyId,
            EventType = dto.EventType,
            TargetUrl = dto.TargetUrl,
            Secret    = secret,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.WebhookSubscriptions.Add(sub);
        await _db.SaveChangesAsync();
        return ToDto(sub);
    }

    /// <summary>
    /// Returns true only for HTTPS URLs that resolve to public (non-RFC-1918) addresses.
    /// Rejects: http://, private ranges (10/8, 172.16/12, 192.168/16), loopback
    /// (127/8, ::1, localhost), link-local (169.254/16 — AWS/Azure IMDS), and
    /// the Alibaba Cloud metadata endpoint (100.100.100.200).
    /// </summary>
    private static bool IsAllowedWebhookUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return false;
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;

        var host = uri.Host.Trim();

        // Reject well-known reserved hostnames
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return false;

        // Try to parse as an IP — reject all private/special ranges
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            // IPv4 checks
            if (bytes.Length == 4)
            {
                // Loopback: 127.0.0.0/8
                if (bytes[0] == 127) return false;
                // RFC-1918: 10.0.0.0/8
                if (bytes[0] == 10)  return false;
                // RFC-1918: 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                // RFC-1918: 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                // Link-local / AWS IMDS: 169.254.0.0/16
                if (bytes[0] == 169 && bytes[1] == 254) return false;
                // Alibaba Cloud metadata: 100.100.100.200
                if (bytes[0] == 100 && bytes[1] == 100 && bytes[2] == 100 && bytes[3] == 200) return false;
                // Broadcast / unspecified
                if (bytes[0] == 0 || bytes[0] == 255) return false;
            }
            // IPv6 loopback
            if (ip.Equals(System.Net.IPAddress.IPv6Loopback)) return false;
            if (ip.IsIPv6LinkLocal) return false;
        }

        return true;
    }

    public async Task<bool> DeleteAsync(int id, int? companyId)
    {
        // FIX AUDIT-07S-02 (privilege escalation): a company-scoped administrator could
        // previously soft-delete GLOBAL subscriptions (CompanyId == null), affecting every
        // tenant. Global subscriptions are now deletable only by an unrestricted (SuperAdmin)
        // caller, i.e. companyId == null. Company admins are strictly limited to their own
        // company's subscriptions. Note: companyId == -1 is the fail-closed sentinel emitted
        // for a malformed/absent company claim and matches nothing.
        var sub = companyId.HasValue
            ? await _db.WebhookSubscriptions.FirstOrDefaultAsync(x =>
                  x.Id == id && x.CompanyId == companyId)
            : await _db.WebhookSubscriptions.FirstOrDefaultAsync(x => x.Id == id);
        if (sub == null) return false;
        sub.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Enqueues webhook jobs onto the bounded channel for reliable background dispatch.
    /// WebhookDispatcherService drains the channel with 3-attempt exponential back-off.
    /// Jobs are not lost on normal shutdown (channel drains before process exits).
    /// </summary>
    public async Task DispatchAsync(string eventType, int? companyId, object payload)
    {
        var subs = await _db.WebhookSubscriptions
            .Where(w => w.IsActive && w.EventType == eventType &&
                        (companyId == null || w.CompanyId == null || w.CompanyId == companyId))
            .ToListAsync();

        var body = JsonSerializer.Serialize(new
        {
            @event    = eventType,
            timestamp = DateTime.UtcNow,
            data      = payload
        });

        foreach (var sub in subs)
        {
            var job = new WebhookJob(sub.TargetUrl, sub.Secret, eventType, body);
            if (!_dispatcher.TryWrite(job))
                _logger.LogWarning(
                    "[Webhook] Channel full — dropping event {Event} for {Url}. " +
                    "Consider increasing channel capacity or investigating subscriber latency.",
                    eventType, sub.TargetUrl);
        }
    }

    private static string ComputeHmac(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ── Test-friendly aliases ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> RegisterWebhookAsync(CreateWebhookDto dto)
    {
        var result = await RegisterAsync(dto.CompanyId, dto);
        return result.Id;
    }

    /// <inheritdoc/>
    public async Task<List<WebhookDto>> GetWebhooksAsync(int? companyId, CancellationToken ct = default)
    {
        var list = await _db.WebhookSubscriptions
            .Where(w => w.IsActive && (companyId == null || w.CompanyId == companyId))
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    /// <inheritdoc/>
    public Task<bool> DeleteWebhookAsync(int id, int? companyId)
        => DeleteAsync(id, companyId);

    /// <inheritdoc/>
    /// Writes a signed <see cref="WebhookOutbox"/> row for each active matching subscription.
    /// The background dispatcher drains the outbox and delivers with retry.
    public async Task DispatchEventAsync(int companyId, string eventType, string payload)
    {
        var subs = await _db.WebhookSubscriptions
            .Where(w => w.IsActive && w.EventType == eventType &&
                        (w.CompanyId == null || w.CompanyId == companyId))
            .ToListAsync();

        foreach (var sub in subs)
        {
            var signature = ComputeHmac(sub.Secret, payload);
            _db.WebhookOutbox.Add(new HRMS.Domain.Entities.Webhook.WebhookOutbox
            {
                CompanyId      = companyId,
                SubscriptionId = sub.Id,
                EventType      = eventType,
                TargetUrl      = sub.TargetUrl,
                Payload        = payload,
                Signature      = signature,
                Status         = "Pending",
                CreatedAt      = DateTime.UtcNow
            });
        }

        if (subs.Count > 0)
            await _db.SaveChangesAsync();
    }

    private static WebhookDto ToDto(WebhookSubscription w) => new()
    {
        Id        = w.Id,
        CompanyId = w.CompanyId,
        EventType = w.EventType,
        TargetUrl = w.TargetUrl,
        IsActive  = w.IsActive,
        CreatedAt = w.CreatedAt
    };
}
