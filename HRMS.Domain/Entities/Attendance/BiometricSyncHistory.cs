using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// Audit record for each biometric sync operation executed by an admin or the background service.
/// Provides a full history of when syncs ran, how many records were processed, and any errors.
/// </summary>
public class BiometricSyncHistory : ICompanyOwned
{
    public int    Id        { get; set; }
    public int?   CompanyId { get; set; }

    /// <summary>Device synced. Null = vendor-level sync (all devices of that vendor).</summary>
    public int? BiometricDeviceId { get; set; }
    public BiometricDevice? Device { get; set; }

    /// <summary>Vendor name used for this sync run.</summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>Start of the date range that was fetched from the device.</summary>
    public DateTime RangeFrom { get; set; }

    /// <summary>End of the date range that was fetched from the device.</summary>
    public DateTime RangeTo { get; set; }

    /// <summary>UTC timestamp when the sync operation started.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the sync operation completed (null if still running).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total raw punch logs fetched from the device.</summary>
    public int TotalFetched { get; set; }

    /// <summary>Number of new WebAttendance records created.</summary>
    public int RecordsCreated { get; set; }

    /// <summary>Number of existing WebAttendance records updated (check-out punch).</summary>
    public int RecordsUpdated { get; set; }

    /// <summary>Number of punches skipped (unknown employee, duplicate, etc.).</summary>
    public int RecordsSkipped { get; set; }

    /// <summary>Whether the sync completed without errors.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Error message if the sync failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether this sync was triggered by the background service (true) or manually by an admin (false).</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>UserId of the admin who triggered a manual sync. Null for automatic syncs.</summary>
    public int? TriggeredByUserId { get; set; }
}
