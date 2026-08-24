using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Timesheet;

public class TimesheetEntry : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string EmployeeId { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string TaskDescription { get; set; } = string.Empty;
    public decimal HoursWorked { get; set; }
    public string Status { get; set; } = "Draft"; // Draft|Submitted|Approved|Rejected
    public string? ManagerRemarks { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
