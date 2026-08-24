using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Expense;

/// <summary>Supporting document attachment on an expense claim (header level).</summary>
public class ExpenseAttachment : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int ExpenseClaimId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ─────────────────────────────────────────────────────────
    public ExpenseClaim ExpenseClaim { get; set; } = null!;
}
