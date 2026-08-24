using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// Known device fingerprint per employee, used for GPS spoof-detection heuristics.
/// A sudden device change for the same employee is flagged for review.
/// </summary>
public class AttendanceDevice : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;

    public string DeviceFingerprint { get; set; } = string.Empty;  // hashed UA + screen dims
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? LastIpAddress { get; set; }
    public bool IsTrusted { get; set; } = true;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public int UseCount { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
