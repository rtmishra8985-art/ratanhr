namespace HRMS.Application.DTOs.Payroll;

/// <summary>Request body for the preview-only /api/payroll/calculate endpoint.</summary>
public class PayrollCalculationRequest
{
    public decimal BasicPay      { get; set; }
    public int     WorkingDays   { get; set; } = 26;
    public int     DaysPresent   { get; set; } = 26;
    public bool    IsMetroCity   { get; set; } = false;
    public string  State         { get; set; } = "Maharashtra";
    public string  TaxRegime     { get; set; } = "new";   // "new" | "old"
    public int     Month         { get; set; } = 1;       // needed for Feb PT slab
    public decimal AdditionalAllowances { get; set; } = 0;

    // ── Old-regime specific fields ────────────────────────────────────────
    // These fields are only used when TaxRegime = "old".
    // They are not used in the new-regime path and default to zero so that
    // callers that do not supply them receive the correct zero deduction
    // rather than a silent wrong value.

    /// <summary>
    /// Annual Section 80C deduction claimed by the employee (e.g. EPF, ELSS, PPF,
    /// life-insurance premiums, home-loan principal). Capped at ₹1,50,000/yr by the
    /// calculator — any amount above this limit is silently clamped.
    /// Only used when TaxRegime = "old". Default: ₹0.
    /// </summary>
    public decimal Section80CDeduction { get; set; } = 0;

    /// <summary>
    /// Rent paid by the employee per month (used to compute HRA exemption under the
    /// old regime). When zero (default) no HRA exemption is claimed, which is
    /// conservative and correct when rent details are unavailable.
    /// Only used when TaxRegime = "old". Default: ₹0.
    /// </summary>
    public decimal RentPaidMonthly { get; set; } = 0;

    // ── Item 5: overtime / bonus / arrears ──────────────────────────────────
    /// <summary>Overtime pay in currency for the period (caller-computed; no rate is configured
    /// anywhere in the system to derive this from attendance minutes). Added to gross and to
    /// taxable income, pro-rated by attendance like other allowances. Default: ₹0.</summary>
    public decimal OvertimePay { get; set; } = 0;
    /// <summary>Taxable bonus for the period. Added to gross and to taxable income, NOT pro-rated
    /// by attendance (a bonus is not an hourly wage component). Default: ₹0.</summary>
    public decimal BonusAmount { get; set; } = 0;
    /// <summary>Prior-period salary correction paid this period. Added to gross and to taxable
    /// income, NOT pro-rated by the current period's attendance factor since it represents
    /// already-earned pay from an earlier period. Default: ₹0.</summary>
    public decimal Arrears { get; set; } = 0;
}

/// <summary>Fully-computed payslip breakdown — all deductions auto-calculated per Indian statutory rules.</summary>
public class PayrollCalculationResult
{
    // Earnings
    public decimal BasicPay           { get; set; }
    public decimal HRA                { get; set; }
    public decimal DA                 { get; set; }
    public decimal Conveyance         { get; set; }
    public decimal MedicalAllowance   { get; set; }
    public decimal OtherAllowances    { get; set; }
    public decimal OvertimePay        { get; set; }
    public decimal BonusAmount        { get; set; }
    public decimal Arrears            { get; set; }
    public decimal GrossEarnings      { get; set; }

    // Deductions
    public decimal PFEmployee         { get; set; }
    public decimal PFEmployer         { get; set; }
    public decimal ESIEmployee        { get; set; }
    public decimal ESIEmployer        { get; set; }
    public decimal ProfessionalTax    { get; set; }
    public decimal TDS                { get; set; }
    public decimal TotalDeductions    { get; set; }
    public decimal NetPay             { get; set; }

    // Breakdown notes
    public string  PFNote             { get; set; } = string.Empty;
    public string  ESINote            { get; set; } = string.Empty;
    public string  TDSNote            { get; set; } = string.Empty;
    public string  PTNote             { get; set; } = string.Empty;
}
