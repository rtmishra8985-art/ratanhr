using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Expense;

/// <summary>Single approval step (Manager or Finance) on an expense claim.</summary>
public class ExpenseApproval : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int ExpenseClaimId { get; set; }

    /// <summary>Manager | Finance</summary>
    public string Step { get; set; } = string.Empty;
    /// <summary>Pending | Approved | Rejected | SendBack</summary>
    public string Status { get; set; } = "Pending";

    public int? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime? ActionAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
}
