using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Performance;

public class EmployeeGoal : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string EmployeeId { get; set; } = string.Empty;
    public int? PerformanceCycleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GoalType { get; set; } = "Individual"; // Individual, Department, Company
    public string Category { get; set; } = "KPI"; // OKR, KPI, Project
    public decimal TargetValue { get; set; }
    public decimal? AchievedValue { get; set; }
    public string Unit { get; set; } = string.Empty; // %, units, calls, etc.
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Not Started"; // Not Started, In Progress, Completed, On Hold
    public int Weight { get; set; } = 100; // percentage weight
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
