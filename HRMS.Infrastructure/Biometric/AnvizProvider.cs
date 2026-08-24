using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Anviz biometric device provider — CrossChex HTTP API implementation.
/// Communicates with Anviz terminals via their local HTTP REST API (port 8080 by default)
/// or the Anviz CrossChex Cloud API. Token-based authentication.
/// API reference: https://www.anviz.com/developer/
/// </summary>
public sealed class AnvizProvider : IBiometricProvider
{
    private readonly ILogger<AnvizProvider> _logger;
    private readonly HttpClient _http;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int MaxFailures  = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    public string VendorName => "Anviz";

    public AnvizProvider(ILogger<AnvizProvider> logger)
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
        $"http://{Environment.GetEnvironmentVariable("ANVIZ_DEVICE_IP") ?? "192.168.1.204"}:" +
        $"{Environment.GetEnvironmentVariable("ANVIZ_DEVICE_PORT") ?? "8080"}";

    private string ApiKey =>
        Environment.GetEnvironmentVariable("ANVIZ_API_KEY") ?? string.Empty;

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        if (!string.IsNullOrEmpty(ApiKey))
            req.Headers.Add("Token", ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return req;
    }

    public async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Anviz] Circuit breaker open — skipping FetchLogsAsync");
            return Array.Empty<BiometricPunchLog>();
        }

        try
        {
            // POST /api/v1/query — returns attendance logs for the specified period
            var req = BuildRequest(HttpMethod.Post, "/api/v1/query", new
            {
                type      = "att_log",
                startTime = from.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime   = to.ToString("yyyy-MM-dd HH:mm:ss")
            });

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("code", out var code) || code.GetInt32() != 0)
            {
                _logger.LogWarning("[Anviz] API returned non-zero code: {Json}", json);
                return Array.Empty<BiometricPunchLog>();
            }

            var logs = new List<BiometricPunchLog>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var entry in data.EnumerateArray())
                {
                    var userId  = entry.TryGetProperty("user_id",    out var uid)  ? uid.GetString()  ?? string.Empty : string.Empty;
                    var timeStr = entry.TryGetProperty("check_time", out var chk)  ? chk.GetString()  : null;
                    var status  = entry.TryGetProperty("status",     out var sts)  ? sts.GetInt32()   : 0;

                    if (!DateTime.TryParse(timeStr, out var ts)) continue;

                    // Anviz status: 0=CheckIn, 1=CheckOut, 4=Overtime-In, 5=Overtime-Out
                    var direction = status == 1 || status == 5
                        ? PunchDirection.CheckOut
                        : PunchDirection.CheckIn;

                    logs.Add(new BiometricPunchLog(UserId: userId, PunchedAt: ts.ToUniversalTime(), Direction: direction, DeviceSerial: null));
                }
            }

            RecordSuccess();
            _logger.LogInformation("[Anviz] FetchLogsAsync returned {Count} records", logs.Count);
            return logs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Anviz] FetchLogsAsync failed");
            return Array.Empty<BiometricPunchLog>();
        }
    }

    public async Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Anviz] Circuit breaker open — skipping SyncUsersAsync");
            return 0;
        }

        var synced = 0;
        try
        {
            foreach (var user in users)
            {
                ct.ThrowIfCancellationRequested();

                var req = BuildRequest(HttpMethod.Post, "/api/v1/user/add", new
                {
                    user_id  = user.UserId,
                    name     = user.Name,
                    card_no  = user.CardNumber ?? string.Empty,
                    dept_id  = 1
                });

                var resp = await _http.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() == 0)
                        synced++;
                }
            }

            RecordSuccess();
            _logger.LogInformation("[Anviz] SyncUsersAsync synced {Count}/{Total} users", synced, users.Count);
            return synced;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Anviz] SyncUsersAsync failed after {Synced} users", synced);
            return synced;
        }
    }

    public async Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        if (IsCircuitOpen())
            return new BiometricDeviceStatus(false, null, null, "Circuit breaker open");

        try
        {
            var req  = BuildRequest(HttpMethod.Get, "/api/v1/device/info");
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            RecordSuccess();
            var root = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
            return new BiometricDeviceStatus(
                IsOnline: true,
                FirmwareVersion: root.TryGetProperty("firmware_version", out var fv) ? fv.GetString() : null,
                EnrolledUserCount: root.TryGetProperty("user_count", out var uc) ? uc.GetInt32() : null,
                LastError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogWarning(ex, "[Anviz] GetDeviceStatusAsync failed");
            return new BiometricDeviceStatus(false, null, null, ex.Message);
        }
    }
}
