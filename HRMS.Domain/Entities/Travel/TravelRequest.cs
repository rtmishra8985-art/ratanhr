using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Travel;

/// <summary>
/// Core travel request entity. Supports full multi-step approval workflow.
/// Statuses: Draft → Submitted → ManagerApproved → HRApproved → FinanceApproved → Completed → Cancelled | Rejected
/// </summary>
public class TravelRequest : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;

    // ── Travel details ─────────────────────────────────────────────────────
    /// <summary>Local | Domestic | International</summary>
    public string TravelType { get; set; } = "Domestic";
    public string Purpose { get; set; } = string.Empty;
    public string FromCity { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;
    /// <summary>Alias for ToCity — tests use Destination.</summary>
    [NotMapped] public string Destination { get => ToCity; set => ToCity = value; }
    public DateTime StartDate { get; set; }
    /// <summary>Alias for StartDate — tests use DepartureDate.</summary>
    [NotMapped] public DateTime DepartureDate { get => StartDate; set => StartDate = value; }
    public DateTime EndDate { get; set; }
    /// <summary>Alias for EndDate — tests use ReturnDate.</summary>
    [NotMapped] public DateTime ReturnDate { get => EndDate; set => EndDate = value; }
    /// <summary>Flight | Train | Bus | Car | Cab | Ship | Other</summary>
    public string ModeOfTravel { get; set; } = "Flight";
    public bool AdvanceRequired { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }

    // ── Workflow state ─────────────────────────────────────────────────────
    /// <summary>Draft | Submitted | ManagerApproved | HRApproved | FinanceApproved | Completed | Rejected | Cancelled</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>UserId of the approver who approved/rejected this request.</summary>
    public int? ApprovedBy { get; set; }

    // ── Soft-delete & audit ────────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────
    public ICollection<TravelApproval> Approvals { get; set; } = new List<TravelApproval>();
    public ICollection<TravelHistory> History { get; set; } = new List<TravelHistory>();
}
