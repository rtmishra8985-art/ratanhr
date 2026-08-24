// Fix: Added ICompanyOwned + CompanyId for tenant-scoped global query filter.
using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Payroll;

public class SalaryStructure : ICompanyOwned
{
    public int    Id          { get; set; }
    public string EmployeeId  { get; set; } = string.Empty;
    /// <summary>
    /// Tenant discriminator. Null = legacy record pre-dating multi-tenancy.
    /// EF Core global query filter scopes all reads to the caller's company.
    /// </summary>
    public int?   CompanyId   { get; set; }
    public decimal CTC                { get; set; } // Cost to Company (annual)
    public decimal BasicPay           { get; set; } // Monthly basic
    public decimal HRA                { get; set; }
    public decimal DA                 { get; set; }
    public decimal Conveyance         { get; set; }
    public decimal MedicalAllowance   { get; set; }
    public decimal OtherAllowances    { get; set; }
    public decimal PFEmployee         { get; set; } // Employee PF contribution
    public decimal PFEmployer         { get; set; } // Employer PF contribution
    public decimal ESI                { get; set; }
    public decimal PT                 { get; set; }
    public decimal TDS                { get; set; }
    public DateOnly  EffectiveFrom    { get; set; }
    public DateOnly? EffectiveTo      { get; set; }
    public bool     IsActive          { get; set; } = true;
    public int      CreatedByUserId   { get; set; }
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;

    // ── Tax regime fields (Phase 1) ────────────────────────────────────────
    // Persisted so that bulk payslip generation uses the regime choice made when
    // the salary structure was created, without requiring a re-calculation.

    /// <summary>
    /// When true the TDS stored in this record was calculated under the old income-tax
    /// regime (pre-Budget slabs). When false (default) the new regime applies.
    /// Maps to the is_old_regime column on salary_structures.
    /// </summary>
    public bool    IsOldRegime         { get; set; } = false;

    /// <summary>
    /// Annual Section 80C deduction claimed by the employee (relevant only when
    /// IsOldRegime = true). Capped at ₹1,50,000 by the payroll calculator.
    /// Maps to the section_80c_deduction column on salary_structures.
    /// </summary>
    public decimal Section80CDeduction { get; set; } = 0m;
}
