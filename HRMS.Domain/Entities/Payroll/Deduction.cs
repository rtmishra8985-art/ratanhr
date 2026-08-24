// Fix: Added ICompanyOwned + CompanyId for tenant-scoped global query filter.
using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Payroll;

public class Deduction : ICompanyOwned
{
    public int    Id             { get; set; }
    public string EmployeeId    { get; set; } = string.Empty;
    /// <summary>
    /// Tenant discriminator. Null = legacy record pre-dating multi-tenancy.
    /// EF Core global query filter scopes all reads to the caller's company.
    /// </summary>
    public int?   CompanyId     { get; set; }
    public string DeductionType  { get; set; } = string.Empty; // Loan, Advance, LOP, Other
    public decimal Amount        { get; set; }
    public int    Month          { get; set; }
    public int    Year           { get; set; }
    public string? Remarks       { get; set; }
    public int    CreatedByUserId { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
}
