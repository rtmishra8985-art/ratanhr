using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// A named geographic boundary an admin uses to validate employee attendance.
/// Types: Office | Factory | Warehouse | Branch | ProjectSite | Store
/// </summary>
public class GeoFence : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    /// <summary>Office | Factory | Warehouse | Branch | ProjectSite | Store</summary>
    public string FenceType { get; set; } = "Office";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Allowed radius in metres. Configurable: 50 | 100 | 200 | 500 | 1000.</summary>
    public double RadiusMetres { get; set; } = 200;

    /// <summary>Optional branch association.</summary>
    public int? BranchId { get; set; }
    /// <summary>Address or landmark description.</summary>
    public string? Address { get; set; }

    /// <summary>
    /// When true employees outside the fence can still check in (admin override).
    /// </summary>
    public bool AllowOutsideCheckin { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────
    public ICollection<GeoFenceHistory> History { get; set; } = new List<GeoFenceHistory>();
    public ICollection<AttendanceGps> GpsLogs { get; set; } = new List<AttendanceGps>();
}
