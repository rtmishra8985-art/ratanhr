using HRMS.Domain.Common;
using CompanyEntity = HRMS.Domain.Entities.Company.Company;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Payroll;

public class Payslip : ICompanyOwned
{
    public int    Id         { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int PayslipId { get => Id; set => Id = value; }
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>
    /// Tenant discriminator. NOT NULL — backfill script must run before migration.
    /// EF Core global query filter scopes all reads to the caller's company.
    /// </summary>
    public int    CompanyId  { get; set; }
    // Explicit interface implementation: ICompanyOwned requires int? but the column is NOT NULL.
    int? ICompanyOwned.CompanyId => CompanyId;
    public int    Month      { get; set; }
    public int    Year       { get; set; }
    public int    WorkingDays  { get; set; }
    public int    DaysPresent  { get; set; }

    // Earnings
    public decimal BasicPay          { get; set; }
    public decimal HRA               { get; set; }
    public decimal DA                { get; set; }
    public decimal Conveyance        { get; set; }
    public decimal MedicalAllowance  { get; set; }
    public decimal OtherAllowances   { get; set; }
    /// <summary>
    /// Item 5 fix: overtime pay in currency for the period. The calculator does not
    /// derive this from WebAttendance.OvertimeMinutes because no OT rate is configured
    /// anywhere in the system (Employee/CompanySettings) — inventing one would be a
    /// guess at business policy. Callers that want OT reflected in payroll must compute
    /// minutes × rate themselves and pass the resulting amount here.
    /// </summary>
    public decimal OvertimePay       { get; set; }
    /// <summary>
    /// Item 5 fix: taxable bonus for the period, auto-summed by PayrollService from the
    /// existing Bonus module (Bonus.Amount where IsTaxable, EmployeeId/Month/Year match)
    /// unless the caller supplies an explicit override.
    /// </summary>
    public decimal BonusAmount       { get; set; }
    /// <summary>
    /// Item 5 fix: prior-period salary correction paid in the current period. No Arrears
    /// entity/table exists elsewhere in the system; this is a manual pass-through amount
    /// on the generate request. Added to gross/taxable income but NOT pro-rated by the
    /// current period's attendance factor, since arrears represent already-earned pay.
    /// </summary>
    public decimal Arrears           { get; set; }
    public decimal GrossEarnings     { get; set; }
    /// <summary>Alias for GrossEarnings — tests use GrossPay.</summary>
    [NotMapped] public decimal GrossPay { get => GrossEarnings; set => GrossEarnings = value; }

    // Deductions
    public decimal PFEmployee        { get; set; }
    /// <summary>Alias for PFEmployee — tests use PfDeduction.</summary>
    [NotMapped] public decimal PfDeduction { get => PFEmployee; set => PFEmployee = value; }
    public decimal PFEmployer        { get; set; }
    public decimal ESI               { get; set; }
    public decimal PT                { get; set; }
    public decimal TDS               { get; set; }
    public decimal OtherDeductions   { get; set; }
    public decimal TotalDeductions   { get; set; }

    public decimal NetPay     { get; set; }
    /// <summary>Alias for NetPay — tests use NetSalary.</summary>
    [NotMapped] public decimal NetSalary { get => NetPay; set => NetPay = value; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Lifecycle status of the payslip.
    /// Values: Draft | Generated | Approved | Cancelled
    /// Default is "Generated" as payslips are created already generated.
    /// </summary>
    public string Status { get; set; } = "Generated";

    /// <summary>
    /// Optimistic concurrency token backed by MySQL row version (TIMESTAMP(6) auto-updated column).
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Tenant navigation. Backs the database-level FK
    /// fk_payslips_company_id -> companies(id) ON DELETE RESTRICT, so tenant
    /// isolation is enforced by the engine and not only by the EF global query
    /// filter. RESTRICT (not CASCADE) because payslips are statutory financial
    /// records that must never be silently removed with a company row.
    /// </summary>
    public CompanyEntity? Company { get; set; }

    /// <summary>
    /// True when this payslip was created by the demo-mode seed service
    /// (<see cref="HRMS.Infrastructure.Services.Demo.DemoSeedService"/>). Used by
    /// CleanupAsync to delete only demo payslips and never touch real payroll records.
    /// </summary>
    public bool IsDemo { get; set; } = false;
}
