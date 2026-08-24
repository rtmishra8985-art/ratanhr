using FluentValidation;
using HRMS.Application.DTOs.Payroll;

namespace HRMS.Application.Validators;

// ── GeneratePayslipDto ─────────────────────────────────────────────────────

public class GeneratePayslipDtoValidator : AbstractValidator<GeneratePayslipDto>
{
    private static readonly string[] ValidTaxRegimes = { "new", "old" };

    public GeneratePayslipDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.WorkingDays)
            .InclusiveBetween(1, 31).WithMessage("Working days must be between 1 and 31.");

        RuleFor(x => x.DaysPresent)
            .InclusiveBetween(0, 31).WithMessage("Days present must be between 0 and 31.")
            .LessThanOrEqualTo(x => x.WorkingDays)
            .WithMessage("Days present cannot exceed working days.");

        RuleFor(x => x.BasicPay)
            .GreaterThanOrEqualTo(0m).WithMessage("Basic pay must not be negative.");

        // Non-auto-calculate: all component amounts must be non-negative
        RuleFor(x => x.Hra).GreaterThanOrEqualTo(0m).WithMessage("HRA must not be negative.");
        RuleFor(x => x.Da).GreaterThanOrEqualTo(0m).WithMessage("DA must not be negative.");
        RuleFor(x => x.Conveyance).GreaterThanOrEqualTo(0m).WithMessage("Conveyance must not be negative.");
        RuleFor(x => x.MedicalAllowance).GreaterThanOrEqualTo(0m).WithMessage("Medical allowance must not be negative.");
        RuleFor(x => x.OtherAllowances).GreaterThanOrEqualTo(0m).WithMessage("Other allowances must not be negative.");
        RuleFor(x => x.PfEmployee).GreaterThanOrEqualTo(0m).WithMessage("PF employee must not be negative.");
        RuleFor(x => x.PfEmployer).GreaterThanOrEqualTo(0m).WithMessage("PF employer must not be negative.");
        RuleFor(x => x.Esi).GreaterThanOrEqualTo(0m).WithMessage("ESI must not be negative.");
        RuleFor(x => x.Pt).GreaterThanOrEqualTo(0m).WithMessage("PT must not be negative.");
        RuleFor(x => x.Tds).GreaterThanOrEqualTo(0m).WithMessage("TDS must not be negative.");
        RuleFor(x => x.OtherDeductions).GreaterThanOrEqualTo(0m).WithMessage("Other deductions must not be negative.");

        RuleFor(x => x.TaxRegime)
            .Must(r => ValidTaxRegimes.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("TaxRegime must be 'new' or 'old'.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxRegime));

        RuleFor(x => x.State)
            .MaximumLength(100).When(x => x.State != null);
    }
}

// ── BulkPayrollDto ─────────────────────────────────────────────────────────

public class BulkPayrollDtoValidator : AbstractValidator<BulkPayrollDto>
{
    public BulkPayrollDtoValidator()
    {
        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.CompanyId)
            .GreaterThan(0).WithMessage("CompanyId must be a positive integer.")
            .When(x => x.CompanyId.HasValue);

        // Working days must be a valid pay-period length (1–31).
        RuleFor(x => x.WorkingDays)
            .InclusiveBetween(1, 31).WithMessage("Working days must be between 1 and 31.");

        // Future payroll is allowed (pre-calculation), but not more than 2 months ahead.
        // Guard: only run when Month and Year are already valid (avoids ArgumentOutOfRangeException
        // from new DateTime() when Month=0 or Year is out of range — those failures are handled
        // by the rules above).
        RuleFor(x => x)
            .Must(dto =>
            {
                var now = DateTime.UtcNow;
                var maxAllowed = new DateTime(now.Year, now.Month, 1).AddMonths(2);
                var requestedDate = new DateTime(dto.Year, dto.Month, 1);
                return requestedDate <= maxAllowed;
            })
            .WithMessage("Cannot generate payroll more than 2 months in the future.")
            .When(x => x.Month is >= 1 and <= 12 && x.Year is >= 2000 and <= 2100);
    }
}

/// <summary>Alias used by tests (BulkPayrollValidator) — identical to BulkPayrollDtoValidator.</summary>
public class BulkPayrollValidator : BulkPayrollDtoValidator { }

// ── PayrollCalculationRequest ──────────────────────────────────────────────

public class PayrollCalculationRequestValidator : AbstractValidator<PayrollCalculationRequest>
{
    private static readonly string[] ValidTaxRegimes = { "new", "old" };

    public PayrollCalculationRequestValidator()
    {
        RuleFor(x => x.BasicPay)
            .GreaterThanOrEqualTo(0m).WithMessage("Basic pay must not be negative.");

        RuleFor(x => x.WorkingDays)
            .InclusiveBetween(1, 31).WithMessage("Working days must be between 1 and 31.");

        RuleFor(x => x.DaysPresent)
            .InclusiveBetween(0, 31).WithMessage("Days present must be between 0 and 31.")
            .LessThanOrEqualTo(x => x.WorkingDays)
            .WithMessage("Days present cannot exceed working days.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.AdditionalAllowances)
            .GreaterThanOrEqualTo(0m).WithMessage("Additional allowances must not be negative.");

        RuleFor(x => x.TaxRegime)
            .Must(r => ValidTaxRegimes.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("TaxRegime must be 'new' or 'old'.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxRegime));
    }
}

// ── CreateSalaryStructureDto ───────────────────────────────────────────────

public class CreateSalaryStructureDtoValidator : AbstractValidator<CreateSalaryStructureDto>
{
    public CreateSalaryStructureDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.CTC)
            .GreaterThanOrEqualTo(0m).WithMessage("CTC must not be negative.");

        RuleFor(x => x.BasicPay)
            .GreaterThan(0m).WithMessage("Basic pay must be greater than zero.")
            .LessThanOrEqualTo(x => x.CTC)
            .WithMessage("Basic pay must not exceed CTC.")
            .When(x => x.CTC > 0);

        RuleFor(x => x.HRA).GreaterThanOrEqualTo(0m).WithMessage("HRA must not be negative.");
        RuleFor(x => x.DA).GreaterThanOrEqualTo(0m).WithMessage("DA must not be negative.");
        RuleFor(x => x.Conveyance).GreaterThanOrEqualTo(0m).WithMessage("Conveyance must not be negative.");
        RuleFor(x => x.MedicalAllowance).GreaterThanOrEqualTo(0m).WithMessage("Medical allowance must not be negative.");
        RuleFor(x => x.OtherAllowances).GreaterThanOrEqualTo(0m).WithMessage("Other allowances must not be negative.");
        RuleFor(x => x.PFEmployee).GreaterThanOrEqualTo(0m).WithMessage("PF employee must not be negative.");
        RuleFor(x => x.PFEmployer).GreaterThanOrEqualTo(0m).WithMessage("PF employer must not be negative.");
        RuleFor(x => x.ESI).GreaterThanOrEqualTo(0m).WithMessage("ESI must not be negative.");
        RuleFor(x => x.PT).GreaterThanOrEqualTo(0m).WithMessage("PT must not be negative.");
        RuleFor(x => x.TDS).GreaterThanOrEqualTo(0m).WithMessage("TDS must not be negative.");

        RuleFor(x => x.EffectiveFrom)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(-5)))
            .WithMessage("Effective-from date must not be more than 5 years in the past.")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(2)))
            .WithMessage("Effective-from date must not be more than 2 years in the future.");

        RuleFor(x => x.CreatedByUserId)
            .GreaterThan(0).WithMessage("CreatedByUserId must be a positive integer.");
    }
}

// ── CreateBonusDto ─────────────────────────────────────────────────────────

public class CreateBonusDtoValidator : AbstractValidator<CreateBonusDto>
{
    public CreateBonusDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.BonusType)
            .NotEmpty().WithMessage("Bonus type is required.")
            .MaximumLength(100).WithMessage("Bonus type must not exceed 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Bonus amount must be greater than zero.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Remarks)
            .MaximumLength(500).When(x => x.Remarks != null);

        RuleFor(x => x.CreatedByUserId)
            .GreaterThan(0).WithMessage("CreatedByUserId must be a positive integer.");
    }
}

// ── CreateDeductionDto ─────────────────────────────────────────────────────

public class CreateDeductionDtoValidator : AbstractValidator<CreateDeductionDto>
{
    public CreateDeductionDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.DeductionType)
            .NotEmpty().WithMessage("Deduction type is required.")
            .MaximumLength(100).WithMessage("Deduction type must not exceed 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Deduction amount must be greater than zero.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Remarks)
            .MaximumLength(500).When(x => x.Remarks != null);

        RuleFor(x => x.CreatedByUserId)
            .GreaterThan(0).WithMessage("CreatedByUserId must be a positive integer.");
    }
}

/// <summary>Alias used by tests (GeneratePayslipValidator) — identical to GeneratePayslipDtoValidator.</summary>
public class GeneratePayslipValidator : GeneratePayslipDtoValidator { }

/// <summary>Alias used by tests (CreateBonusValidator) — identical to CreateBonusDtoValidator.</summary>
public class CreateBonusValidator : CreateBonusDtoValidator { }

/// <summary>Alias used by tests (CreateDeductionValidator) — identical to CreateDeductionDtoValidator.</summary>
public class CreateDeductionValidator : CreateDeductionDtoValidator { }
