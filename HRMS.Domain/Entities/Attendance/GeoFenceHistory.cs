using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>Immutable change log for geofence configuration updates.</summary>
public class GeoFenceHistory : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int GeoFenceId { get; set; }

    public string Action { get; set; } = string.Empty;  // Created | Updated | Deleted
    public string? ChangedBy { get; set; }
    public string? ChangeDetails { get; set; }           // JSON snapshot of changed fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public GeoFence GeoFence { get; set; } = null!;
}
