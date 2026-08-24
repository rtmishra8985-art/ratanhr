using System.Net.Http.Headers;
using System.Text.Json;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Matrix COSEC biometric device provider — HTTP REST implementation.
/// Communicates with the Matrix COSEC Day Panel via its REST API (default port 4050).
/// API documentation: Matrix COSEC REST API Guide v2.x
/// </summary>
public sealed class MatrixProvider : IBiometricProvider
{
    private readonly ILogger<MatrixProvider> _logger;
    private readonly HttpClient _http;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int MaxFailures  = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    public string VendorName => "Matrix";

    public MatrixProvider(ILogger<MatrixProvider> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // Basic auth is applied per request using the device credentials
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
        $"http://{Environment.GetEnvironmentVariable("MATRIX_DEVICE_IP") ?? ""}:" +
        $"{Environment.GetEnvironmentVariable("MATRIX_DEVICE_PORT") ?? "4050"}";

    private AuthenticationHeaderValue BasicAuth => new("Basic",
        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(
            $"{Environment.GetEnvironmentVariable("MATRIX_USERNAME") ?? ""}:" +
            $"{Environment.GetEnvironmentVariable("MATRIX_PASSWORD") ?? ""}")));

    public async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Matrix] Circuit breaker open — skipping FetchLogsAsync");
            return Array.Empty<BiometricPunchLog>();
        }

        try
        {
            // Matrix COSEC REST endpoint: GET /Att/Query
            // Returns JSON array: [{userId, dateTime, dir}]
            var url = $"{BaseUrl}/Att/Query?userId=All" +
                      $"&fromDate={from:yyyy-MM-ddTHH:mm:ss}" +
                      $"&toDate={to:yyyy-MM-ddTHH:mm:ss}&format=json";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = BasicAuth;

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var records = JsonSerializer.Deserialize<List<MatrixAttLog>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<MatrixAttLog>();

            var logs = records.Select(r => new BiometricPunchLog(
                UserId:       r.UserId ?? string.Empty,
                PunchedAt:    DateTime.TryParse(r.DateTime, out var ts) ? ts.ToUniversalTime() : DateTime.UtcNow,
                Direction:    string.Equals(r.Dir, "OUT", StringComparison.OrdinalIgnoreCase)
                              ? PunchDirection.CheckOut : PunchDirection.CheckIn,
                DeviceSerial: null))
                .ToList();

            RecordSuccess();
            _logger.LogInformation("[Matrix] FetchLogsAsync returned {Count} records", logs.Count);
            return logs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Matrix] FetchLogsAsync failed");
            return Array.Empty<BiometricPunchLog>();
        }
    }

    public async Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[Matrix] Circuit breaker open — skipping SyncUsersAsync");
            return 0;
        }

        var synced = 0;
        try
        {
            foreach (var user in users)
            {
                ct.ThrowIfCancellationRequested();
                // POST /User/Add?format=json
                var url = $"{BaseUrl}/User/Add?format=json";
                var payload = JsonSerializer.Serialize(new
                {
                    userId   = user.UserId,
                    name     = user.Name,
                    cardNo   = user.CardNumber ?? string.Empty,
                    privilege = 0
                });

                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = BasicAuth;

                var resp = await _http.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode) synced++;
            }

            RecordSuccess();
            _logger.LogInformation("[Matrix] SyncUsersAsync synced {Count}/{Total} users", synced, users.Count);
            return synced;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogError(ex, "[Matrix] SyncUsersAsync failed after {Synced} users", synced);
            return synced;
        }
    }

    public async Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        if (IsCircuitOpen())
            return new BiometricDeviceStatus(false, null, null, "Circuit breaker open");

        try
        {
            var url = $"{BaseUrl}/Device/GetInfo?format=json";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = BasicAuth;

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            RecordSuccess();
            return new BiometricDeviceStatus(
                IsOnline: true,
                FirmwareVersion: root.TryGetProperty("firmwareVersion", out var fv) ? fv.GetString() : null,
                EnrolledUserCount: root.TryGetProperty("userCount", out var uc) ? uc.GetInt32() : null,
                LastError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            _logger.LogWarning(ex, "[Matrix] GetDeviceStatusAsync failed");
            return new BiometricDeviceStatus(false, null, null, ex.Message);
        }
    }

    // ── DTO for deserialisation ───────────────────────────────────────────
    private sealed class MatrixAttLog
    {
        public string? UserId   { get; set; }
        public string? DateTime { get; set; }
        public string? Dir      { get; set; }
    }
}
