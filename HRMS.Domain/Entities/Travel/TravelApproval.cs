using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Travel;

/// <summary>
/// Tracks each approval step in the travel request workflow.
/// Step: Manager | HR | Finance
/// </summary>
public class TravelApproval : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int TravelRequestId { get; set; }

    /// <summary>Manager | HR | Finance</summary>
    public string Step { get; set; } = string.Empty;
    /// <summary>Pending | Approved | Rejected | SendBack</summary>
    public string Status { get; set; } = "Pending";

    public int? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime? ActionAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public TravelRequest TravelRequest { get; set; } = null!;
}
