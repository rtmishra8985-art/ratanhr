namespace HRMS.Application.DTOs.Payroll;

public class SalaryStructureDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public decimal CTC { get; set; }
    public decimal BasicPay { get; set; }
    public decimal HRA { get; set; }
    public decimal DA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal PFEmployee { get; set; }
    public decimal PFEmployer { get; set; }
    public decimal ESI { get; set; }
    public decimal PT { get; set; }
    public decimal TDS { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Tax regime (Phase 1)
    /// <summary>True when TDS was calculated under the old income-tax regime.</summary>
    public bool    IsOldRegime         { get; set; } = false;
    /// <summary>Annual 80C deduction used when IsOldRegime = true. Zero for new-regime records.</summary>
    public decimal Section80CDeduction { get; set; } = 0m;
}

public class CreateSalaryStructureDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public decimal CTC { get; set; }
    public decimal BasicPay { get; set; }
    public decimal HRA { get; set; }
    public decimal DA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal PFEmployee { get; set; }
    public decimal PFEmployer { get; set; }
    public decimal ESI { get; set; }
    public decimal PT { get; set; }
    public decimal TDS { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public int CreatedByUserId { get; set; }

    // Tax regime (Phase 1)
    /// <summary>
    /// Pass true to calculate and store TDS under the old income-tax regime.
    /// The payroll service uses this to set PayrollCalculationRequest.TaxRegime = "old"
    /// and to persist IsOldRegime on the resulting SalaryStructure record.
    /// Default: false (new regime).
    /// </summary>
    public bool    IsOldRegime         { get; set; } = false;

    /// <summary>
    /// Annual Section 80C deduction for the employee. Only used when IsOldRegime = true.
    /// Capped at ₹1,50,000 by the payroll calculator. Default: ₹0.
    /// </summary>
    public decimal Section80CDeduction { get; set; } = 0m;
}

public class BonusDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string BonusType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string? Remarks { get; set; }
    public bool IsTaxable { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBonusDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string BonusType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string? Remarks { get; set; }
    public bool IsTaxable { get; set; } = true;
    public int CreatedByUserId { get; set; }

    /// <summary>Tenant discriminator — tests and security checks pass CompanyId in the DTO.</summary>
    public int? CompanyId { get; set; }
}

public class DeductionDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string DeductionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDeductionDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string DeductionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string? Remarks { get; set; }
    public int CreatedByUserId { get; set; }

    /// <summary>Tenant discriminator — tests and security checks pass CompanyId in the DTO.</summary>
    public int? CompanyId { get; set; }
}

/// <summary>Shared payslip summary DTO used in Payroll controllers and Reports.</summary>
public class PayslipListDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Tenant discriminator — needed for IDOR scoping in list endpoints.</summary>
    public int? CompanyId { get; set; }

    public int Month { get; set; }
    public int Year { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    /// <summary>Alias for NetPay — tests use NetSalary.</summary>
    public decimal NetSalary { get => NetPay; set => NetPay = value; }

    public int DaysPresent { get; set; }
    public int WorkingDays { get; set; }
}

public class PayslipDetailDto : PayslipListDto
{
    public decimal BasicPay { get; set; }
    public decimal HRA { get; set; }
    public decimal DA { get; set; }
    public decimal Conveyance { get; set; }
    public decimal MedicalAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal PFEmployee { get; set; }
    public decimal PFEmployer { get; set; }
    public decimal ESI { get; set; }
    public decimal PT { get; set; }
    public decimal TDS { get; set; }
    public decimal OtherDeductions { get; set; }
}
