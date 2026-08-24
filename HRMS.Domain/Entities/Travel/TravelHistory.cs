using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Travel;

/// <summary>Immutable audit trail for all status changes on a travel request.</summary>
public class TravelHistory : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int TravelRequestId { get; set; }

    public string Action { get; set; } = string.Empty;   // e.g. "Submitted", "Approved by Manager"
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? PerformedBy { get; set; }              // UserId or EmployeeId
    public string? PerformedByName { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public TravelRequest TravelRequest { get; set; } = null!;
}
