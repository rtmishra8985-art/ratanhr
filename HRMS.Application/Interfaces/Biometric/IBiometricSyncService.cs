namespace HRMS.Application.Interfaces.Biometric;

/// <summary>
/// Orchestrates biometric punch log sync from a device into HRMS attendance records.
/// </summary>
public interface IBiometricSyncService
{
    /// <summary>
    /// Fetches logs from the device identified by <paramref name="vendorName"/> and
    /// upserts them as attendance records for the given company.
    /// Returns the count of records synced.
    /// </summary>
    Task<int> SyncAttendanceAsync(
        string vendorName,
        int companyId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the connection status for a given vendor's device.
    /// </summary>
    Task<BiometricDeviceStatus> GetDeviceStatusAsync(
        string vendorName,
        CancellationToken ct = default);
}
