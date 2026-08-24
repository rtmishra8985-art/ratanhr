using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Leave;

/// <summary>Leave category (Casual, Sick, Earned/Privilege, etc.) with an annual quota.</summary>
public class LeaveType : ICompanyOwned
{
    public int Id { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int LeaveTypeId { get => Id; set => Id = value; }
    public int? CompanyId { get; set; } // null = applies to all companies (global default)
    public string Name { get; set; } = string.Empty;
    public int AnnualQuotaDays { get; set; }
    /// <summary>Alias for AnnualQuotaDays — tests use Quota.</summary>
    [NotMapped] public int Quota { get => AnnualQuotaDays; set => AnnualQuotaDays = value; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
