using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// GPS metadata captured at the moment of web check-in or check-out.
/// Extends (but never replaces) the existing WebAttendance record.
/// </summary>
public class AttendanceGps : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }

    /// <summary>FK to existing WebAttendance.Id</summary>
    public int WebAttendanceId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;

    // ── GPS coordinates ────────────────────────────────────────────────────
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>GPS accuracy in metres reported by the browser.</summary>
    public double? Accuracy { get; set; }
    /// <summary>CheckIn | CheckOut</summary>
    public string EventType { get; set; } = "CheckIn";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // ── Geofence evaluation ────────────────────────────────────────────────
    public int? GeoFenceId { get; set; }
    public double? DistanceMetres { get; set; }
    /// <summary>Whether the employee was inside the matched geofence at event time.</summary>
    public bool IsInsideGeofence { get; set; }

    // ── Device / network context ───────────────────────────────────────────
    public string? DeviceType { get; set; }   // desktop | mobile | tablet
    public string? Browser { get; set; }
    public string? IpAddress { get; set; }
    public string? Network { get; set; }      // wifi | 4g | 5g | unknown
    public double? BatteryLevel { get; set; } // 0–100; null if browser doesn't support
    public string? GpsStatus { get; set; }    // granted | denied | unavailable

    // ── Audit ──────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public GeoFence? GeoFence { get; set; }
}
