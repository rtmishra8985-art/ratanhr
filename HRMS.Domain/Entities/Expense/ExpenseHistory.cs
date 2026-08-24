using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Expense;

/// <summary>Immutable audit trail for all status changes on an expense claim.</summary>
public class ExpenseHistory : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int ExpenseClaimId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
}
