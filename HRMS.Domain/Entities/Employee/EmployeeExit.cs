using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Employee;

public class EmployeeExit : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string ExitType { get; set; } = string.Empty; // Resignation, Termination, Retirement, Absconding
    public int? NoticePeriodDays { get; set; }
    public DateOnly? ResignationDate { get; set; }
    public DateOnly? LastWorkingDate { get; set; }
    public string? Reason { get; set; }
    public string? ExitReason { get; set; }
    public string? InterviewNotes { get; set; }
    public bool IsNoticePeriodServed { get; set; }
    public bool IsCompleted { get; set; }
    public decimal? GratuityAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
    public string Status { get; set; } = "Initiated"; // Initiated, InProgress, Completed
    public int InitiatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
