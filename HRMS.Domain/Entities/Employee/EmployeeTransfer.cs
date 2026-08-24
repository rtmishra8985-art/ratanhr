using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Employee;

public class EmployeeTransfer : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? FromDepartment { get; set; }
    public string? ToDepartment { get; set; }
    public string? FromDesignation { get; set; }
    public string? ToDesignation { get; set; }
    public int? FromCompanyId { get; set; }
    public int? ToCompanyId { get; set; }
    public int? FromBranchId { get; set; }
    public int? ToBranchId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
