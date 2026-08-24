using FluentValidation;
using HRMS.Application.DTOs.Leave;

namespace HRMS.Application.Validators;

// ── ApplyLeaveDto ──────────────────────────────────────────────────────────

public class ApplyLeaveDtoValidator : AbstractValidator<ApplyLeaveDto>
{
    public ApplyLeaveDtoValidator()
    {
        RuleFor(x => x.LeaveTypeId)
            .GreaterThan(0).WithMessage("LeaveTypeId must be a positive integer.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.")
            .Must(BeValidDate).WithMessage("Start date must be a valid date in yyyy-MM-dd format.")
            // Employees may not apply for leave more than 1 year in the past
            .Must(d => !DateOnly.TryParse(d, out var dt) || dt >= DateOnly.FromDateTime(DateTime.Today.AddYears(-1)))
            .WithMessage("Leave start date cannot be more than 1 year in the past.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .Must(BeValidDate).WithMessage("End date must be a valid date in yyyy-MM-dd format.")
            .Must((dto, endDate) =>
            {
                if (!DateOnly.TryParse(dto.StartDate, out var start)) return true;
                if (!DateOnly.TryParse(endDate, out var end)) return true;
                return end >= start;
            })
            .WithMessage("End date must be on or after the start date.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.")
            .When(x => x.Reason != null);
    }

    private static bool BeValidDate(string? value)
        => !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out _);
}

// ── CreateLeaveTypeDto ─────────────────────────────────────────────────────

public class CreateLeaveTypeDtoValidator : AbstractValidator<CreateLeaveTypeDto>
{
    public CreateLeaveTypeDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Leave type name is required.")
            .MaximumLength(100).WithMessage("Leave type name must not exceed 100 characters.");

        // Validate via the Quota alias so that tests using x.Quota work correctly.
        // Quota and AnnualQuotaDays are the same backing field.
        RuleFor(x => x.Quota)
            .InclusiveBetween(1, 365).WithMessage("Annual quota days must be between 1 and 365.");
    }
}

/// <summary>Alias used by tests (CreateLeaveTypeValidator) — identical to CreateLeaveTypeDtoValidator.</summary>
public class CreateLeaveTypeValidator : CreateLeaveTypeDtoValidator { }

// ── LeaveDecisionDto ───────────────────────────────────────────────────────

public class LeaveDecisionDtoValidator : AbstractValidator<LeaveDecisionDto>
{
    public LeaveDecisionDtoValidator()
    {
        RuleFor(x => x.Remarks)
            .MaximumLength(500).WithMessage("Remarks must not exceed 500 characters.")
            .When(x => x.Remarks != null);
    }
}

// ── CreateLeaveBalanceAdjustmentDto ────────────────────────────────────────
// Extends existing DataAnnotations with FluentValidation rules.

public class CreateLeaveBalanceAdjustmentDtoValidator : AbstractValidator<CreateLeaveBalanceAdjustmentDto>
{
    public CreateLeaveBalanceAdjustmentDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.LeaveTypeId)
            .GreaterThan(0).WithMessage("LeaveTypeId must be a positive integer.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.Days)
            .InclusiveBetween(-365, 365).WithMessage("Days must be between -365 and 365.")
            .NotEqual(0).WithMessage("Days adjustment must not be zero.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MinimumLength(5).WithMessage("Reason must be at least 5 characters.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

// ── LeaveCarryForwardDto ───────────────────────────────────────────────────

public class LeaveCarryForwardDtoValidator : AbstractValidator<LeaveCarryForwardDto>
{
    public LeaveCarryForwardDtoValidator()
    {
        RuleFor(x => x.FromYear)
            .InclusiveBetween(2000, 2100).WithMessage("FromYear must be between 2000 and 2100.");

        RuleFor(x => x.ToYear)
            .InclusiveBetween(2000, 2100).WithMessage("ToYear must be between 2000 and 2100.")
            .GreaterThan(x => x.FromYear).WithMessage("ToYear must be greater than FromYear.");

        RuleFor(x => x.MaxDays)
            .GreaterThanOrEqualTo(0).WithMessage("MaxDays must be 0 (unlimited) or a positive integer.");

        RuleFor(x => x.CompanyId)
            .GreaterThan(0).WithMessage("CompanyId must be a positive integer.")
            .When(x => x.CompanyId.HasValue);
    }
}

/// <summary>Alias used by tests (ApplyLeaveValidator) — identical to ApplyLeaveDtoValidator.</summary>
public class ApplyLeaveValidator : ApplyLeaveDtoValidator { }

/// <summary>Alias used by tests (LeaveCarryForwardValidator) — identical to LeaveCarryForwardDtoValidator.</summary>
public class LeaveCarryForwardValidator : LeaveCarryForwardDtoValidator { }

/// <summary>
/// Validator for the read/update <see cref="HRMS.Application.DTOs.Leave.LeaveBalanceAdjustmentDto"/>.
/// (Distinct from CreateLeaveBalanceAdjustmentDtoValidator which targets the create DTO.)
/// </summary>
public class LeaveBalanceAdjustmentValidator : AbstractValidator<LeaveBalanceAdjustmentDto>
{
    public LeaveBalanceAdjustmentValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.");

        RuleFor(x => x.LeaveTypeId)
            .GreaterThan(0).WithMessage("LeaveTypeId must be a positive integer.");

        RuleFor(x => x.Days)
            .NotEqual(0).WithMessage("Days must not be zero.");
    }
}
