using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// Audit record for every GPS attendance attempt — including failed / out-of-fence attempts.
/// Used for security analysis and the "Outside Radius Report".
/// </summary>
public class AttendanceLocationAudit : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }

    public int? GeoFenceId { get; set; }
    public double? DistanceMetres { get; set; }
    public bool IsInsideGeofence { get; set; }
    /// <summary>Whether the check-in was ultimately allowed (could be outside fence but admin override enabled).</summary>
    public bool WasAllowed { get; set; }
    /// <summary>CheckIn | CheckOut | AttemptDenied</summary>
    public string EventType { get; set; } = "CheckIn";

    public string? IpAddress { get; set; }
    public string? Browser { get; set; }
    public string? DeviceType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
