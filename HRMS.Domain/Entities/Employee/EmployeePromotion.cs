using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Employee;

public class EmployeePromotion : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public decimal? SalaryIncrement { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
