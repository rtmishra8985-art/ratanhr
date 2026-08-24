using System.Net.Http.Headers;
using System.Text;
using System.Web;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// eSSL biometric device provider — HTTP REST implementation.
/// Communicates with the eSSL device via its built-in HTTP server (PUSH protocol / cdata API).
/// Port is typically 8080; device must be configured in PUSH mode with this server as the host,
/// or alternatively this provider polls the device directly.
/// </summary>
public sealed class EsslProvider : IBiometricProvider
{
    private readonly ILogger<EsslProvider> _logger;
    private readonly HttpClient _http;

    // Circuit-breaker state (same pattern as ZKTecoProvider)
    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int MaxFailures  = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    public string VendorName => "eSSL";

    public EsslProvider(ILogger<EsslProvider> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ── Circuit breaker ────────────────────────────────────────────────────

    private bool IsCircuitOpen()
    {
        if (_consecutiveFailures < MaxFailures) return false;
        if (DateTime.UtcNow < _circuitOpenUntil) return true;
        // Half-open: allow one attempt through
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

    // ── IBiometricProvider ────────────────────────────────────────────────

    public async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[eSSL] Circuit breaker open — skipping FetchLogsAsync");
            return Array.Empty<BiometricPunchLog>();
        }

        try
        {
            // eSSL devices respond to HTTP GET on /iclock/getrequest?SN=<serial>&options=all
            // and emit attendance records in the format: C/Att\t<uid>\t<datetime>\t<status>\r\n
            // We request the log via the device's local HTTP server.
            var uri = BuildUri("iclock", "getrequest", new Dictionary<string, string>
            {
                ["options"]   = "att",
                ["Stamp"]     = from.ToString("O"),
            });

            var response = await _http.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);

            var logs = ParseAttLogs(body, from, to);
            RecordSuccess();
            _logger.LogInformation("[eSSL] FetchLogsAsync returned {Count} records", logs.Count);
            return logs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[eSSL] FetchLogsAsync failed");
            return Array.Empty<BiometricPunchLog>();
        }
    }

    public async Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[eSSL] Circuit breaker open — skipping SyncUsersAsync");
            return 0;
        }

        var synced = 0;
        try
        {
            // eSSL uses a cdata POST to push user records to the device
            var uri = BuildUri("iclock", "cdata", new Dictionary<string, string>
            {
                ["SN"]      = "HRMS_SYNC",
                ["options"] = "user",
            });

            var sb = new StringBuilder();
            foreach (var u in users)
            {
                // Format: Name=<name>\tPin=<userId>\tPrivilege=0\r\n
                sb.AppendLine($"Name={u.Name}\tPin={u.UserId}\tPrivilege=0\tPassword=\tCard=");
                synced++;
            }

            var content = new StringContent(sb.ToString(), Encoding.ASCII, "text/plain");
            var resp = await _http.PostAsync(uri, content, ct);
            resp.EnsureSuccessStatusCode();

            RecordSuccess();
            _logger.LogInformation("[eSSL] SyncUsersAsync synced {Count} users", synced);
            return synced;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[eSSL] SyncUsersAsync failed after {Synced} users", synced);
            return synced;
        }
    }

    public async Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            return new BiometricDeviceStatus(
                IsOnline: false,
                FirmwareVersion: null,
                EnrolledUserCount: null,
                LastError: "Circuit breaker open");
        }

        try
        {
            var uri = BuildUri("iclock", "deviceinfo");
            var resp = await _http.GetAsync(uri, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);

            // Parse key=value pairs from device info response
            var info = body.Split(new[] { '\r', '\n', '&' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(l => l.Split('=', 2))
                           .Where(p => p.Length == 2)
                           .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            RecordSuccess();
            return new BiometricDeviceStatus(
                IsOnline: true,
                FirmwareVersion: info.GetValueOrDefault("FirmVer"),
                EnrolledUserCount: info.TryGetValue("UserCount", out var uc) && int.TryParse(uc, out var n) ? n : null,
                LastError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogWarning(ex, "[eSSL] GetDeviceStatusAsync failed");
            return new BiometricDeviceStatus(IsOnline: false, FirmwareVersion: null, EnrolledUserCount: null, LastError: ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Builds a URI using device connection values supplied by the environment.</summary>
    private static string BuildUri(string path, string endpoint, Dictionary<string, string>? qs = null)
    {
        // Device-specific values must be supplied through Replit Secrets/environment variables.
        // Never fall back to a sample or guessed address: that could send credentials or
        // attendance traffic to an unintended device.
        var ip = Environment.GetEnvironmentVariable("ESSL_DEVICE_IP");
        var port = Environment.GetEnvironmentVariable("ESSL_DEVICE_PORT");
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(port))
            throw new InvalidOperationException(
                "eSSL device configuration is incomplete. Set ESSL_DEVICE_IP and ESSL_DEVICE_PORT via the environment.");

        var query = qs != null && qs.Count > 0
            ? "?" + string.Join("&", qs.Select(kv => $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}"))
            : string.Empty;

        return $"http://{ip}:{port}/{path}/{endpoint}{query}";
    }

    private static List<BiometricPunchLog> ParseAttLogs(string body, DateTime from, DateTime to)
    {
        var logs = new List<BiometricPunchLog>();
        foreach (var line in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Expected format: C/Att\t<uid>\t<datetime YYYY-MM-DD HH:mm:ss>\t<status>
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            if (!parts[0].StartsWith("C/", StringComparison.OrdinalIgnoreCase)) continue;

            var uid = parts[1].Trim();
            if (!DateTime.TryParse(parts[2].Trim(), out var ts)) continue;
            if (ts < from || ts > to) continue;

            // Status: 0=CheckIn, 1=CheckOut (varies by device model)
            var status = parts.Length > 3 && parts[3].Trim() == "1"
                ? PunchDirection.CheckOut
                : PunchDirection.CheckIn;

            logs.Add(new BiometricPunchLog(UserId: uid, PunchedAt: ts.ToUniversalTime(), Direction: status, DeviceSerial: null));
        }
        return logs;
    }
}
