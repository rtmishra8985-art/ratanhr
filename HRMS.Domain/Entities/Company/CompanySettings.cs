using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Company;

public class CompanySettings : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int WorkingDaysPerMonth { get; set; } = 26;
    public decimal PFPercentage { get; set; } = 12m;
    public decimal ESIPercentage { get; set; } = 0.75m;
    public decimal PTAmount { get; set; } = 200m;
    public string? PayslipFooterNote { get; set; }
    public string? TimeZone { get; set; } = "Asia/Kolkata";
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public int? OvertimeThresholdMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Company? Company { get; set; }
}
