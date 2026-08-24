using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Expense;

/// <summary>
/// Individual line-item within an expense claim.
/// Categories: Hotel | Flight | Cab | Fuel | Food | Train | Bus | Miscellaneous
/// </summary>
public class ExpenseItem : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int ExpenseClaimId { get; set; }

    /// <summary>Hotel | Flight | Cab | Fuel | Food | Train | Bus | Miscellaneous</summary>
    public string Category { get; set; } = "Miscellaneous";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
}
