namespace HRMS.Application.DTOs.Leave;

public class LeaveTypeDto
{
    public int Id { get; set; }

    /// <summary>Domain-prefixed PK alias — tests assert LeaveTypeId.</summary>
    public int LeaveTypeId { get => Id; set => Id = value; }

    public int? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int AnnualQuotaDays { get; set; }

    /// <summary>Alias for AnnualQuotaDays — tests use Quota.</summary>
    public int Quota { get => AnnualQuotaDays; set => AnnualQuotaDays = value; }

    public bool IsPaid { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLeaveTypeDto
{
    public string Name { get; set; } = string.Empty;
    public int AnnualQuotaDays { get; set; }

    /// <summary>Alias for AnnualQuotaDays — tests and validators target Quota.</summary>
    public int Quota { get => AnnualQuotaDays; set => AnnualQuotaDays = value; }

    public bool IsPaid { get; set; } = true;
}

public class ApplyLeaveDto
{
    /// <summary>Employee making the request — new-style tests embed EmployeeId in the DTO.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    public int LeaveTypeId { get; set; }
    public string StartDate { get; set; } = string.Empty; // yyyy-MM-dd
    public string EndDate { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class LeaveDecisionDto
{
    public bool Approve { get; set; }
    public string? Remarks { get; set; }
}

public class LeaveRequestDto
{
    public int Id { get; set; }

    /// <summary>Domain-prefixed PK alias — tests assert LeaveRequestId.</summary>
    public int LeaveRequestId { get => Id; set => Id = value; }

    public string  EmployeeId     { get; set; } = string.Empty;
    public string? EmployeeName   { get; set; }
    /// <summary>Phase 1 – D: CompanyId added to enable IDOR scoping in GetById/Decide/Cancel endpoints.</summary>
    public int?    CompanyId      { get; set; }
    public int     LeaveTypeId    { get; set; }
    public string  LeaveTypeName  { get; set; } = string.Empty;
    public string  StartDate      { get; set; } = string.Empty;  // yyyy-MM-dd
    public string  EndDate        { get; set; } = string.Empty;
    public int     TotalDays      { get; set; }
    public string? Reason         { get; set; }
    public string  Status         { get; set; } = string.Empty;
    public string? ApproverRemarks { get; set; }
    public DateTime CreatedAt     { get; set; }
}

public class LeaveBalanceDto
{
    public int LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public int AnnualQuotaDays { get; set; }
    public int AnnualQuota { get => AnnualQuotaDays; set => AnnualQuotaDays = value; }
    public bool IsPaid { get; set; }
    public int UsedDays { get; set; }
    public int PendingDays { get; set; }
    public int RemainingDays { get; set; }
}
