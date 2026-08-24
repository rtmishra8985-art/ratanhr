using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Hikvision biometric device provider — ISAPI HTTP implementation.
/// Communicates with Hikvision access-control terminals via ISAPI (HTTP REST with Digest auth).
/// ISAPI reference: Hikvision ISAPI Development Guide
/// Default port: 80 (HTTP) or 443 (HTTPS).
/// </summary>
public sealed class HikvisionProvider : IBiometricProvider
{
    private readonly ILogger<HikvisionProvider> _logger;
    private readonly HttpClient _http;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int MaxFailures  = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    public string VendorName => "Hikvision";

    public HikvisionProvider(ILogger<HikvisionProvider> logger)
    {
        _logger = logger;
        // Hikvision ISAPI uses Digest authentication.
        // Credentials MUST be supplied via env vars; empty strings force a connection failure
        // rather than silently authenticating with a default credential.
        var credentials = new NetworkCredential(
            Environment.GetEnvironmentVariable("HIKVISION_USERNAME") ?? "",
            Environment.GetEnvironmentVariable("HIKVISION_PASSWORD") ?? "");

        // SECURITY FIX (CRITICAL): Certificate validation is ENABLED by default.
        // DangerousAcceptAnyServerCertificateValidator has been removed — it accepted
        // any certificate including forged ones, opening a full MitM attack vector on
        // biometric attendance data.
        //
        // Set HIKVISION_SKIP_CERT_VALIDATION=true ONLY in isolated private-network
        // environments where Hikvision devices use self-signed certificates that
        // cannot be replaced (e.g. on-premise LAN with no CA infrastructure).
        // Never enable this in internet-facing or multi-tenant deployments.
        var skipCertValidation = string.Equals(
            Environment.GetEnvironmentVariable("HIKVISION_SKIP_CERT_VALIDATION"), "true",
            StringComparison.OrdinalIgnoreCase);

        if (skipCertValidation)
            _logger.LogWarning(
                "[Hikvision] HIKVISION_SKIP_CERT_VALIDATION=true — TLS certificate validation " +
                "is DISABLED. This must only be used on isolated private networks where devices " +
                "cannot be provisioned with a trusted certificate.");

        var handler = new HttpClientHandler
        {
            Credentials     = credentials,
            PreAuthenticate = false,
            ServerCertificateCustomValidationCallback = skipCertValidation
                ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                : null   // null = default OS/runtime validation (correct for production)
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
        $"http://{Environment.GetEnvironmentVariable("HIKVISION_DEVICE_IP") ?? ""}";

    public async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Hikvision] Circuit breaker open — skipping FetchLogsAsync");
            return Array.Empty<BiometricPunchLog>();
        }

        try
        {
            // ISAPI endpoint: GET /ISAPI/AccessControl/AcsEvent?format=json
            // Body: SearchEvent XML or JSON with time range
            var url = $"{BaseUrl}/ISAPI/AccessControl/AcsEvent?format=json";

            var requestBody = JsonSerializer.Serialize(new
            {
                AcsEventCond = new
                {
                    searchID           = Guid.NewGuid().ToString("N")[..8],
                    searchResultPosition = 0,
                    maxResults         = 1000,
                    major              = 5,  // 5 = access event
                    minor              = 75, // 75 = fingerprint/card success
                    startTime          = from.ToString("yyyy-MM-ddTHH:mm:ss+00:00"),
                    endTime            = to.ToString("yyyy-MM-ddTHH:mm:ss+00:00")
                }
            });

            var resp = await _http.PostAsync(url,
                new StringContent(requestBody, Encoding.UTF8, "application/json"), ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var logs = new List<BiometricPunchLog>();
            if (doc.RootElement.TryGetProperty("AcsEvent", out var events) &&
                events.TryGetProperty("InfoList", out var infoList))
            {
                foreach (var e in infoList.EnumerateArray())
                {
                    var userId = e.TryGetProperty("employeeNoString", out var uid)
                        ? uid.GetString() ?? string.Empty
                        : (e.TryGetProperty("cardNo", out var card) ? card.GetString() ?? string.Empty : string.Empty);

                    var timeStr = e.TryGetProperty("time", out var t) ? t.GetString() : null;
                    if (!DateTime.TryParse(timeStr, out var ts)) continue;

                    // Hikvision doesn't distinguish in/out on basic events; use serialNo parity as heuristic
                    var serialNo = e.TryGetProperty("serialNo", out var sn) ? sn.GetInt64() : 0;
                    var direction = serialNo % 2 == 0 ? PunchDirection.CheckIn : PunchDirection.CheckOut;

                    logs.Add(new BiometricPunchLog(UserId: userId, PunchedAt: ts.ToUniversalTime(), Direction: direction, DeviceSerial: null));
                }
            }

            RecordSuccess();
            _logger.LogInformation("[Hikvision] FetchLogsAsync returned {Count} records", logs.Count);
            return logs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Hikvision] FetchLogsAsync failed");
            return Array.Empty<BiometricPunchLog>();
        }
    }

    public async Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Hikvision] Circuit breaker open — skipping SyncUsersAsync");
            return 0;
        }

        var synced = 0;
        try
        {
            // Hikvision ISAPI: PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json
            var url = $"{BaseUrl}/ISAPI/AccessControl/UserInfo/SetUp?format=json";

            var payload = JsonSerializer.Serialize(new
            {
                UserInfo = users.Select(u => new
                {
                    employeeNo   = u.UserId,
                    name         = u.Name,
                    userType     = "normal",
                    Valid        = new { beginTime = "2020-01-01T00:00:00", endTime = "2030-12-31T23:59:59" },
                    doorRight    = "1",
                    RightPlan    = new[] { new { doorNo = 1, planTemplateNo = "1" } }
                }).ToArray()
            });

            var resp = await _http.PutAsync(url,
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            if (resp.IsSuccessStatusCode) synced = users.Count;

            RecordSuccess();
            _logger.LogInformation("[Hikvision] SyncUsersAsync synced {Count} users", synced);
            return synced;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Hikvision] SyncUsersAsync failed");
            return synced;
        }
    }

    public async Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        if (IsCircuitOpen())
            return new BiometricDeviceStatus(false, null, null, "Circuit breaker open");

        try
        {
            var url  = $"{BaseUrl}/ISAPI/System/status?format=json";
            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            RecordSuccess();
            return new BiometricDeviceStatus(
                IsOnline: true,
                FirmwareVersion: root.TryGetProperty("firmwareVersion", out var fv) ? fv.GetString() : null,
                EnrolledUserCount: null,
                LastError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogWarning(ex, "[Hikvision] GetDeviceStatusAsync failed");
            return new BiometricDeviceStatus(false, null, null, ex.Message);
        }
    }
}
