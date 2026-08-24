using HRMS.Domain.Common;
namespace HRMS.Domain.Entities;

/// <summary>Company-specific or global public holiday.</summary>
public class HolidayCalendar : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }          // null = global/all companies
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public bool IsOptional { get; set; } = false; // true = optional holiday
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
