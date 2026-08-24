using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Analytics;

public class AnalyticsSnapshot : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string SnapshotType { get; set; } = string.Empty; // "headcount"|"turnover"|"attendance"|"payroll"
    public string Period { get; set; } = string.Empty;        // "2025-06"
    public decimal Value { get; set; }
    public string? Metadata { get; set; }                     // JSON blob
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
