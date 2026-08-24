using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Leave;

/// <summary>Admin-initiated manual adjustment to an employee's leave balance.</summary>
public class LeaveBalanceAdjustment : ICompanyOwned
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public int Days { get; set; }              // positive = credit, negative = debit
    public string Reason { get; set; } = string.Empty;
    public int AdjustedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
