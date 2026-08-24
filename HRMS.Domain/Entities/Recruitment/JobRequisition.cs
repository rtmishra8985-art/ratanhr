using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Recruitment;

public class JobRequisition : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OpeningsCount { get; set; } = 1;
    public string ExperienceRequired { get; set; } = string.Empty;
    public string SkillsRequired { get; set; } = string.Empty;
    public string Status { get; set; } = "Open"; // Open, In Progress, Closed, On Hold
    public string JobType { get; set; } = "Full-Time"; // Full-Time, Part-Time, Contract, Internship
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime? ClosingDate { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
