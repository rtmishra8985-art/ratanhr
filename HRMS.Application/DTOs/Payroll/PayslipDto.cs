namespace HRMS.Application.DTOs.Payroll;

public class GeneratePayslipDto
{
    public string  EmployeeId        { get; set; } = string.Empty;
    public int     Month             { get; set; }
    public int     Year              { get; set; }
    /// <summary>Tenant discriminator — required for IDOR scoping and payslip generation.</summary>
    public int?    CompanyId         { get; set; }
    public int     WorkingDays       { get; set; } = 26;
    public int     DaysPresent       { get; set; } = 26;

    // Earnings (required when AutoCalculate = false; optional otherwise)
    public decimal BasicPay          { get; set; }
    public decimal Hra               { get; set; }
    public decimal Da                { get; set; }
    public decimal Conveyance        { get; set; }
    public decimal MedicalAllowance  { get; set; }
    public decimal OtherAllowances   { get; set; }

    // ── Item 5: overtime / bonus / arrears ──────────────────────────────────
    /// <summary>Overtime pay in currency. Manual pass-through — no OT rate is configured
    /// anywhere in the system to auto-derive this from WebAttendance.OvertimeMinutes.</summary>
    public decimal OvertimePay       { get; set; }
    /// <summary>
    /// Taxable bonus for the period. When AutoCalculate = true and this is left at its
    /// default (0), PayrollService auto-sums the employee's taxable Bonus records for
    /// this Month/Year from the existing Bonus module. Set explicitly to override.
    /// </summary>
    public decimal BonusAmount       { get; set; }
    /// <summary>Prior-period salary correction paid this period. Manual pass-through —
    /// no Arrears entity/table exists elsewhere in the system.</summary>
    public decimal Arrears           { get; set; }

    // Deductions (required when AutoCalculate = false)
    public decimal PfEmployee        { get; set; }
    public decimal PfEmployer        { get; set; }
    public decimal Esi               { get; set; }
    public decimal Pt                { get; set; }
    public decimal Tds               { get; set; }
    public decimal OtherDeductions   { get; set; }

    // ── Auto-calculation (Indian statutory rules) ──────────────────────────
    /// <summary>
    /// When true, only BasicPay (+ optionally OtherAllowances) are required.
    /// The API computes HRA, DA, Conveyance, Medical, PF, ESI, PT and TDS
    /// automatically using IndianPayrollCalculator.
    /// Defaults to <c>true</c>: a caller that omits this flag would otherwise get a
    /// payslip with zero PF/ESI/PT/TDS, which is a statutory-compliance defect.
    /// Set it explicitly to <c>false</c> to supply every component manually.
    /// </summary>
    public bool   AutoCalculate      { get; set; } = true;

    /// <summary>
    /// FIX BLOCKER-6: When true, allows regenerating a payslip that already exists
    /// for this employee+period. When false (default), the service rejects duplicate
    /// generation with InvalidOperationException. Forces caller intent to be explicit
    /// rather than silently overwriting an existing payslip.
    /// </summary>
    public bool   Overwrite          { get; set; } = false;

    public bool   IsMetroCity        { get; set; } = false;
    public string State              { get; set; } = "Maharashtra";
    public string TaxRegime          { get; set; } = "new";
}

public class PayslipDto
{
    public int     Id               { get; set; }

    /// <summary>Domain-prefixed PK alias — tests assert PayslipId.</summary>
    public int     PayslipId        { get => Id; set => Id = value; }

    /// <summary>Tenant discriminator — required for IDOR scoping.</summary>
    public int?    CompanyId        { get; set; }

    /// <summary>Payslip lifecycle status: Draft | Generated | Approved | Cancelled.</summary>
    public string  Status           { get; set; } = "Generated";

    public string  EmployeeId       { get; set; } = string.Empty;
    public string  EmployeeName     { get; set; } = string.Empty;
    public string  Designation      { get; set; } = string.Empty;
    public string  Department       { get; set; } = string.Empty;
    public string  BankName         { get; set; } = string.Empty;
    public string  AccountNumber    { get; set; } = string.Empty;
    public string  UAN              { get; set; } = string.Empty;
    public string  MonthYear        { get; set; } = string.Empty;
    public int     Month            { get; set; }
    public int     Year             { get; set; }
    public int     WorkingDays      { get; set; }
    public int     DaysPresent      { get; set; }
    public decimal BasicPay         { get; set; }
    public decimal HRA              { get; set; }
    public decimal DA               { get; set; }
    public decimal Conveyance       { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal OtherAllowances  { get; set; }
    public decimal OvertimePay      { get; set; }
    public decimal BonusAmount      { get; set; }
    public decimal Arrears          { get; set; }
    public decimal GrossEarnings    { get; set; }

    /// <summary>Alias for GrossEarnings — tests use GrossPay.</summary>
    public decimal GrossPay         { get => GrossEarnings; set => GrossEarnings = value; }

    public decimal PFEmployee       { get; set; }
    /// <summary>Alias for PFEmployee — tests use PfDeduction.</summary>
    public decimal PfDeduction      { get => PFEmployee; set => PFEmployee = value; }
    public decimal PFEmployer       { get; set; }
    public decimal ESI              { get; set; }
    public decimal PT               { get; set; }
    public decimal TDS              { get; set; }
    public decimal OtherDeductions  { get; set; }
    public decimal TotalDeductions  { get; set; }
    public decimal NetPay           { get; set; }

    /// <summary>Alias for NetPay — tests use NetSalary.</summary>
    public decimal NetSalary        { get => NetPay; set => NetPay = value; }

    public DateTime CreatedAt       { get; set; }
    public string? CompanyName      { get; set; }
    public string? CompanyLogo      { get; set; }
}
