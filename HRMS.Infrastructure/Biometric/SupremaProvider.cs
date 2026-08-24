using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Suprema biometric device provider — BioStar2 REST API v2 implementation.
/// Communicates with the BioStar2 server (runs on Windows, exposes REST on port 80/443).
/// API reference: https://bs2api.biostar2.com/
/// Authentication: session-based (POST /api/login → User-Session token).
/// </summary>
public sealed class SupremaProvider : IBiometricProvider
{
    private readonly ILogger<SupremaProvider> _logger;
    private readonly HttpClient _http;

    private string? _sessionToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int MaxFailures  = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    public string VendorName => "Suprema";

    public SupremaProvider(ILogger<SupremaProvider> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private bool IsCircuitOpen()
    {
        if (_consecutiveFailures < MaxFailures) return false;
        if (DateTime.UtcNow < _circuitOpenUntil) return true;
        _consecutiveFailures = 0;
        return false;
    }

    private void RecordSuccess() => _consecutiveFailures = 0;

    private void RecordFailure()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= MaxFailures)
            _circuitOpenUntil = DateTime.UtcNow.Add(OpenDuration);
    }

    private string BaseUrl =>
        $"http://{Environment.GetEnvironmentVariable("SUPREMA_SERVER_IP") ?? "192.168.1.202"}:" +
        $"{Environment.GetEnvironmentVariable("SUPREMA_SERVER_PORT") ?? "80"}";

    // ── Session management ────────────────────────────────────────────────

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_sessionToken != null && DateTime.UtcNow < _tokenExpiry) return;

        var loginUrl = $"{BaseUrl}/api/login";
        var payload  = JsonSerializer.Serialize(new
        {
            login_id = Environment.GetEnvironmentVariable("SUPREMA_LOGIN_ID") ?? "",
            password = Environment.GetEnvironmentVariable("SUPREMA_PASSWORD") ?? ""
        });

        var resp = await _http.PostAsync(loginUrl,
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        // BioStar2 returns the session token in the response header "bs-session-id"
        if (resp.Headers.TryGetValues("bs-session-id", out var values))
        {
            _sessionToken = values.FirstOrDefault();
            _tokenExpiry  = DateTime.UtcNow.AddHours(1); // sessions last ~1 h
        }

        _logger.LogInformation("[Suprema] Authenticated with BioStar2 server");
    }

    private void ApplySessionHeader(HttpRequestMessage req)
    {
        if (_sessionToken != null)
            req.Headers.Add("bs-session-id", _sessionToken);
    }

    // ── IBiometricProvider ────────────────────────────────────────────────

    public async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Suprema] Circuit breaker open — skipping FetchLogsAsync");
            return Array.Empty<BiometricPunchLog>();
        }

        try
        {
            await EnsureAuthenticatedAsync(ct);

            // GET /api/events?limit=1000&start_datetime=...&end_datetime=...&event_type_id=1000
            // event_type_id 1000 = Access Granted (attendance punch)
            var url = $"{BaseUrl}/api/events?limit=1000" +
                      $"&start_datetime={from:yyyy-MM-ddTHH:mm:ss}" +
                      $"&end_datetime={to:yyyy-MM-ddTHH:mm:ss}" +
                      $"&event_type_id=1000";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplySessionHeader(req);

            var resp = await _http.SendAsync(req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Re-authenticate and retry once
                _sessionToken = null;
                await EnsureAuthenticatedAsync(ct);
                req = new HttpRequestMessage(HttpMethod.Get, url);
                ApplySessionHeader(req);
                resp = await _http.SendAsync(req, ct);
            }
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var logs = new List<BiometricPunchLog>();
            if (doc.RootElement.TryGetProperty("EventCollection", out var col) &&
                col.TryGetProperty("rows", out var rows))
            {
                foreach (var e in rows.EnumerateArray())
                {
                    var userId   = e.TryGetProperty("user_id",  out var u)  ? u.GetProperty("uid").GetString() ?? string.Empty : string.Empty;
                    var dtStr    = e.TryGetProperty("datetime", out var dt) ? dt.GetString() : null;
                    var isBs2In  = e.TryGetProperty("event_type_id", out var ev) && ev.GetProperty("id").GetInt32() == 1000;

                    if (!DateTime.TryParse(dtStr, out var ts)) continue;

                    logs.Add(new BiometricPunchLog(
                        UserId:       userId,
                        PunchedAt:    ts.ToUniversalTime(),
                        Direction:    isBs2In ? PunchDirection.CheckIn : PunchDirection.CheckOut,
                        DeviceSerial: null));
                }
            }

            RecordSuccess();
            _logger.LogInformation("[Suprema] FetchLogsAsync returned {Count} records", logs.Count);
            return logs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Suprema] FetchLogsAsync failed");
            return Array.Empty<BiometricPunchLog>();
        }
    }

    public async Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Suprema] Circuit breaker open — skipping SyncUsersAsync");
            return 0;
        }

        var synced = 0;
        try
        {
            await EnsureAuthenticatedAsync(ct);

            foreach (var user in users)
            {
                ct.ThrowIfCancellationRequested();

                var url = $"{BaseUrl}/api/users";
                var payload = JsonSerializer.Serialize(new
                {
                    User = new
                    {
                        user_id    = user.UserId,
                        name       = user.Name,
                        login_id   = user.UserId,
                        user_group_id = new { id = "1" }
                    }
                });

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                ApplySessionHeader(req);

                var resp = await _http.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode) synced++;
            }

            RecordSuccess();
            _logger.LogInformation("[Suprema] SyncUsersAsync synced {Count}/{Total} users", synced, users.Count);
            return synced;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Suprema] SyncUsersAsync failed after {Synced} users", synced);
            return synced;
        }
    }

    public async Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        if (IsCircuitOpen())
            return new BiometricDeviceStatus(false, null, null, "Circuit breaker open");

        try
        {
            await EnsureAuthenticatedAsync(ct);

            var deviceId = Environment.GetEnvironmentVariable("SUPREMA_DEVICE_ID") ?? "1";
            var url      = $"{BaseUrl}/api/devices/{deviceId}/status";
            var req      = new HttpRequestMessage(HttpMethod.Get, url);
            ApplySessionHeader(req);

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            RecordSuccess();
            return new BiometricDeviceStatus(
                IsOnline: true,
                FirmwareVersion: root.TryGetProperty("firmware_version", out var fv) ? fv.GetString() : null,
                EnrolledUserCount: root.TryGetProperty("user_count", out var uc) ? uc.GetInt32() : null,
                LastError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogWarning(ex, "[Suprema] GetDeviceStatusAsync failed");
            return new BiometricDeviceStatus(false, null, null, ex.Message);
        }
    }
}
