namespace HRMS.Application.DTOs.Timesheet;

/// <summary>
/// Read-only projection of the weekly-aggregate Timesheet entity.
/// </summary>
public class TimesheetDto
{
    public int     TimesheetId   { get; set; }
    public string  EmployeeId    { get; set; } = string.Empty;
    public int?    CompanyId     { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate   { get; set; }
    public decimal TotalHours    { get; set; }
    public string  Status        { get; set; } = string.Empty;
    public string? ManagerRemarks { get; set; }
    public int?    ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt  { get; set; }
    public DateTime  CreatedAt   { get; set; }
    public DateTime  UpdatedAt   { get; set; }
}

public class TimesheetEntryDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string TaskDescription { get; set; } = string.Empty;
    public decimal HoursWorked { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ManagerRemarks { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTimesheetDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string TaskDescription { get; set; } = string.Empty;
    public decimal HoursWorked { get; set; }
}

public class TimesheetRejectDto
{
    public string Remarks { get; set; } = string.Empty;
}
