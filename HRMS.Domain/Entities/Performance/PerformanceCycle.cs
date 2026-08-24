using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Performance;

public class PerformanceCycle : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string Name { get; set; } = string.Empty; // e.g. "Q4 2024 Annual Review"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Active, Closed
    public string ReviewType { get; set; } = "Annual"; // Annual, Semi-Annual, Quarterly, Project-based
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
