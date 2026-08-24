namespace HRMS.Application.DTOs.Expense;

// ── Expense Claim (header) ─────────────────────────────────────────────────────

public class CreateExpenseClaimDto
{
    public string Title { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
    public int? TravelRequestId { get; set; }
    public string? Notes { get; set; }
    public List<CreateExpenseItemDto> Items { get; set; } = new();
}

public class ExpenseDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int? TravelRequestId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalGst { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ExpenseItemDto> Items { get; set; } = new();
    public List<ExpenseAttachmentDto> Attachments { get; set; } = new();
    public List<ExpenseApprovalDto> Approvals { get; set; } = new();
    public List<ExpenseHistoryDto> History { get; set; } = new();
}

// ── Expense Item (line item) ───────────────────────────────────────────────────

public class CreateExpenseItemDto
{
    /// <summary>Hotel | Flight | Cab | Fuel | Food | Train | Bus | Miscellaneous</summary>
    public string Category { get; set; } = "Miscellaneous";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateOnly ExpenseDate { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? Receipt { get; set; }
}

public class ExpenseItemDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal GstAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Attachments ────────────────────────────────────────────────────────────────

public class ExpenseAttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Approvals & History ────────────────────────────────────────────────────────

public class ExpenseApprovalDto
{
    public int Id { get; set; }
    public string Step { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime? ActionAt { get; set; }
}

public class ExpenseHistoryDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? PerformedByName { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Decision ──────────────────────────────────────────────────────────────────

public class ExpenseDecisionDto
{
    /// <summary>Manager | Finance</summary>
    public string Step { get; set; } = "Manager";
    public bool Approve { get; set; }
    public bool SendBack { get; set; }
    public string? Comments { get; set; }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

public class ExpenseDashboardDto
{
    public int TotalClaims { get; set; }
    public int PendingApproval { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public decimal CurrentMonthAmount { get; set; }
    public List<ExpenseMonthlyStatDto> MonthlyTrend { get; set; } = new();
    public List<ExpenseCategoryStatDto> ByCategory { get; set; } = new();
}

public class ExpenseMonthlyStatDto
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ClaimCount { get; set; }
}

public class ExpenseCategoryStatDto
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

// ── Reports ───────────────────────────────────────────────────────────────────

public class ExpenseReportFilterDto
{
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? EmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

// ── Legacy shim (kept for backward-compat with existing code that used CreateExpenseDto) ──
[Obsolete("Use CreateExpenseClaimDto instead")]
public class CreateExpenseDto
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string? Category { get; set; }
    public string? Notes { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? Receipt { get; set; }
}
