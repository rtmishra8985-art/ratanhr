namespace HRMS.Application.DTOs.Employee;

// ── Document ──────────────────────────────────────────────────────────────
public class EmployeeDocumentDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Notes { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadDocumentDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

// ── Transfer ──────────────────────────────────────────────────────────────
public class EmployeeTransferDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTransferDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public int? FromCompanyId { get; set; }
    public int? ToCompanyId { get; set; }
    public int? FromBranchId { get; set; }
    public int? ToBranchId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
}

// ── Promotion ─────────────────────────────────────────────────────────────
public class EmployeePromotionDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public decimal? SalaryIncrement { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePromotionDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public decimal? SalaryIncrement { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public int CreatedByUserId { get; set; }
}

// ── Exit ──────────────────────────────────────────────────────────────────
public class EmployeeExitDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string ExitType { get; set; } = string.Empty;
    public DateOnly? ResignationDate { get; set; }
    public DateOnly? LastWorkingDate { get; set; }
    public string? Reason { get; set; }
    public string? InterviewNotes { get; set; }
    public bool IsNoticePeriodServed { get; set; }
    public decimal? GratuityAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class InitiateExitDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string ExitType { get; set; } = string.Empty;
    public DateOnly? ResignationDate { get; set; }
    public DateOnly? LastWorkingDate { get; set; }
    public string? Reason { get; set; }
    public bool IsNoticePeriodServed { get; set; }
    public int InitiatedByUserId { get; set; }
}

public class CompleteExitDto
{
    public string? InterviewNotes { get; set; }
    public decimal? GratuityAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
}

// ── Status update request (moved from EmployeeController) ─────────────────
public class UpdateStatusRequest
{
    public bool IsActive { get; set; }
}
