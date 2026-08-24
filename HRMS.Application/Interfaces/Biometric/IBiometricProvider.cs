namespace HRMS.Application.Interfaces.Biometric;

/// <summary>
/// Vendor-agnostic biometric device abstraction.
/// Implement this interface for each hardware vendor (ZKTeco, eSSL, Matrix, Suprema, etc.)
/// and register via DI using the BiometricProviderFactory.
/// </summary>
public interface IBiometricProvider
{
    /// <summary>Human-readable vendor name (e.g. "ZKTeco", "eSSL").</summary>
    string VendorName { get; }

    /// <summary>
    /// Fetches raw attendance punch logs from the device for a given date range.
    /// Returns an empty list (not null) when no records exist.
    /// </summary>
    Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Pushes the full employee roster to the device so it recognises
    /// enrolled users. Returns the number of users successfully synced.
    /// </summary>
    Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default);

    /// <summary>
    /// Queries device connectivity and returns a status snapshot.
    /// Implementations must not throw — return a disconnected status on failure.
    /// </summary>
    Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default);
}

/// <summary>A single attendance punch record from a biometric device.</summary>
public sealed record BiometricPunchLog(
    string UserId,
    DateTime PunchedAt,
    PunchDirection Direction,
    string? DeviceSerial);

/// <summary>User record pushed to a biometric device during sync.</summary>
public sealed record BiometricUser(
    string UserId,
    string Name,
    string? CardNumber);

/// <summary>Device health/connectivity snapshot.</summary>
public sealed record BiometricDeviceStatus(
    bool IsOnline,
    string? FirmwareVersion,
    int? EnrolledUserCount,
    string? LastError);

/// <summary>Direction of a biometric punch (Check-In or Check-Out).</summary>
public enum PunchDirection { CheckIn, CheckOut, Unknown }
