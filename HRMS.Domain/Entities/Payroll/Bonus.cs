// Fix: Added ICompanyOwned + CompanyId for tenant-scoped global query filter.
using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Payroll;

public class Bonus : ICompanyOwned
{
    public int    Id             { get; set; }
    public string EmployeeId    { get; set; } = string.Empty;
    /// <summary>
    /// Tenant discriminator. Null = legacy record pre-dating multi-tenancy.
    /// EF Core global query filter scopes all reads to the caller's company.
    /// </summary>
    public int?   CompanyId     { get; set; }
    public string BonusType     { get; set; } = string.Empty; // Festival, Performance, Joining, etc.
    public decimal Amount       { get; set; }
    public int    Month         { get; set; }
    public int    Year          { get; set; }
    public string? Remarks      { get; set; }
    public bool   IsTaxable     { get; set; } = true;
    public int    CreatedByUserId { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
}
