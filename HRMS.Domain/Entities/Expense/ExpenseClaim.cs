using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Expense;

/// <summary>
/// Header-level expense claim. Line items live in ExpenseItem.
/// Statuses: Draft | Submitted | ManagerApproved | FinanceApproved | Rejected | SendBack
/// </summary>
public class ExpenseClaim : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    /// <summary>Reference to a travel request, if this is a post-travel claim.</summary>
    public int? TravelRequestId { get; set; }
    public string? Notes { get; set; }

    // ── Computed totals (updated by service on each item save) ─────────────
    public decimal TotalAmount { get; set; }
    public decimal TotalGst { get; set; }

    // ── Workflow state ─────────────────────────────────────────────────────
    /// <summary>Draft | Submitted | ManagerApproved | FinanceApproved | Rejected | SendBack</summary>
    public string Status { get; set; } = "Draft";
    public DateTime? SubmittedAt { get; set; }

    // ── Soft-delete & audit ────────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ─────────────────────────────────────────────────────────
    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();
    public ICollection<ExpenseApproval> Approvals { get; set; } = new List<ExpenseApproval>();
    public ICollection<ExpenseHistory> History { get; set; } = new List<ExpenseHistory>();
}
