using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// FIX GAP-HD-02: In-memory fake biometric provider for use in unit and integration tests.
///
/// <para>
/// Register this provider in test DI setup instead of real hardware providers so tests
/// can exercise attendance sync, biometric log processing, and dashboard code paths
/// without requiring physical ZKTeco/eSSL/Hikvision devices or network connectivity.
/// </para>
///
/// <para>Usage in tests:</para>
/// <code>
///   services.AddSingleton&lt;IBiometricProvider, FakeBiometricProvider&gt;();
///   // Or via factory:
///   services.AddSingleton&lt;IBiometricProviderFactory&gt;(_ =>
///       new BiometricProviderFactory(new[] { new FakeBiometricProvider() }));
/// </code>
///
/// <para>
/// The provider is deterministic: FetchLogsAsync returns pre-seeded punch records
/// between the requested date range. SyncUsersAsync always reports success.
/// GetDeviceStatusAsync always reports the device as online.
/// </para>
///
/// <para>
/// Use <see cref="SeedLogs"/> to pre-populate specific punch records before a test,
/// and <see cref="Reset"/> to clear state between tests.
/// </para>
/// </summary>
public sealed class FakeBiometricProvider : IBiometricProvider
{
    private readonly List<BiometricPunchLog> _seededLogs = new();
    private readonly List<BiometricUser>     _syncedUsers = new();

    private bool   _isOnline          = true;
    private string _firmwareVersion   = "FAKE-1.0";
    private string? _simulatedError   = null;

    /// <summary>Human-readable vendor name used for DI factory lookup.</summary>
    public string VendorName => "Fake";

    // ── Configuration helpers (call from test setup) ──────────────────────

    /// <summary>
    /// Adds punch log records that will be returned by <see cref="FetchLogsAsync"/>.
    /// Records are filtered by the requested date range at fetch time.
    /// </summary>
    public FakeBiometricProvider SeedLogs(IEnumerable<BiometricPunchLog> logs)
    {
        _seededLogs.AddRange(logs);
        return this;
    }

    /// <summary>
    /// Adds a single punch log record for a specific user and timestamp.
    /// Convenience overload for simple test scenarios.
    /// </summary>
    public FakeBiometricProvider SeedLog(
        string userId,
        DateTime punchedAt,
        PunchDirection direction = PunchDirection.CheckIn,
        string? deviceSerial    = "FAKE-SN-001")
    {
        _seededLogs.Add(new BiometricPunchLog(userId, punchedAt, direction, deviceSerial));
        return this;
    }

    /// <summary>
    /// Configures the device status returned by <see cref="GetDeviceStatusAsync"/>.
    /// </summary>
    public FakeBiometricProvider ConfigureStatus(
        bool isOnline          = true,
        string firmwareVersion = "FAKE-1.0",
        string? lastError      = null)
    {
        _isOnline         = isOnline;
        _firmwareVersion  = firmwareVersion;
        _simulatedError   = lastError;
        return this;
    }

    /// <summary>
    /// Clears all seeded punch logs and synced user records.
    /// Call between tests to ensure isolation.
    /// </summary>
    public void Reset()
    {
        _seededLogs.Clear();
        _syncedUsers.Clear();
        _isOnline       = true;
        _firmwareVersion = "FAKE-1.0";
        _simulatedError = null;
    }

    // ── Inspection helpers (assert in tests) ──────────────────────────────

    /// <summary>Returns all users that were pushed via <see cref="SyncUsersAsync"/>.</summary>
    public IReadOnlyList<BiometricUser> SyncedUsers => _syncedUsers.AsReadOnly();

    /// <summary>Total number of punch log records currently seeded.</summary>
    public int SeededLogCount => _seededLogs.Count;

    // ── IBiometricProvider implementation ─────────────────────────────────

    /// <inheritdoc/>
    public Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var result = _seededLogs
            .Where(l => l.PunchedAt >= from && l.PunchedAt <= to)
            .OrderBy(l => l.PunchedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<BiometricPunchLog>>(result);
    }

    /// <inheritdoc/>
    public Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        _syncedUsers.AddRange(users);
        return Task.FromResult(users.Count);
    }

    /// <inheritdoc/>
    public Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        var status = new BiometricDeviceStatus(
            IsOnline:          _isOnline,
            FirmwareVersion:   _firmwareVersion,
            EnrolledUserCount: _seededLogs.Select(l => l.UserId).Distinct().Count(),
            LastError:         _simulatedError);

        return Task.FromResult(status);
    }
}
