using HRMS.Domain.Common;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// Persisted raw punch log record fetched from a biometric device.
/// Each row represents one punch event captured from a specific device.
/// Duplicate punch detection is handled at the sync service level before insertion.
/// </summary>
public class BiometricLog : ICompanyOwned
{
    public int    Id        { get; set; }
    public int?   CompanyId { get; set; }

    /// <summary>FK to the device this log was fetched from.</summary>
    public int BiometricDeviceId { get; set; }
    public BiometricDevice? Device { get; set; }

    /// <summary>Biometric user ID as reported by the device (matches Employee.EmployeeId).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>UTC date-time of the punch event on the device clock.</summary>
    public DateTime PunchedAt { get; set; }

    /// <summary>Direction of the punch (Check-In, Check-Out, Unknown).</summary>
    public PunchDirection Direction { get; set; }

    /// <summary>Device serial number at the time of fetch.</summary>
    public string? DeviceSerial { get; set; }

    /// <summary>Whether this log was successfully matched to a WebAttendance record.</summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// FK to the WebAttendance record created or updated from this log.
    /// Null when IsProcessed = false (unknown employee or duplicate skipped).
    /// </summary>
    public int? WebAttendanceId { get; set; }

    /// <summary>Reason this log was skipped if IsProcessed = false (e.g. "Unknown employee").</summary>
    public string? SkipReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
