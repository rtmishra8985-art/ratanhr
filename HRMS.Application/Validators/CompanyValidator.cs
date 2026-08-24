using FluentValidation;
using HRMS.Application.DTOs.Company;

namespace HRMS.Application.Validators;

// ── CreateCompanyDto ───────────────────────────────────────────────────────

public class CreateCompanyDtoValidator : AbstractValidator<CreateCompanyDto>
{
    public CreateCompanyDtoValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.CompanyFounderName)
            .MaximumLength(200).When(x => x.CompanyFounderName != null);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).When(x => x.PhoneNumber != null)
            .Matches(@"^\+?[\d\s\-\(\)]{7,20}$")
                .WithMessage("Phone number must be a valid phone number.")
                .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.EmailAddress)
            .EmailAddress().WithMessage("Email address must be valid.")
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.EmailAddress));

        RuleFor(x => x.IndustryType).MaximumLength(100).When(x => x.IndustryType != null);
        RuleFor(x => x.BusinessType).MaximumLength(100).When(x => x.BusinessType != null);

        // CIN: 21-character alphanumeric (Indian Company Identification Number)
        RuleFor(x => x.CIN)
            .Length(21).WithMessage("CIN must be exactly 21 characters.")
            .Matches(@"^[A-Z0-9]{21}$").WithMessage("CIN must contain only uppercase letters and digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.CIN));

        // PAN: 10-character alphanumeric (Indian format: AAAAA9999A)
        RuleFor(x => x.PAN)
            .Length(10).WithMessage("PAN must be exactly 10 characters.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$").WithMessage("PAN format is invalid (expected: AAAAA9999A).")
            .When(x => !string.IsNullOrWhiteSpace(x.PAN));

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).When(x => x.PostalCode != null);

        RuleFor(x => x.Country)
            .MaximumLength(100).When(x => x.Country != null);
    }
}

// ── CreateCompanyBranchDto ─────────────────────────────────────────────────

public class CreateCompanyBranchDtoValidator : AbstractValidator<CreateCompanyBranchDto>
{
    public CreateCompanyBranchDtoValidator()
    {
        RuleFor(x => x.CompanyId)
            .GreaterThan(0).WithMessage("CompanyId must be a positive integer.");

        RuleFor(x => x.BranchName)
            .NotEmpty().WithMessage("Branch name is required.")
            .MaximumLength(200).WithMessage("Branch name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Branch email must be valid.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[\d\s\-\(\)]{7,20}$").WithMessage("Phone number must be a valid phone number.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.PostalCode).MaximumLength(20).When(x => x.PostalCode != null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City != null);
        RuleFor(x => x.StateProvince).MaximumLength(100).When(x => x.StateProvince != null);
        RuleFor(x => x.Country).MaximumLength(100).When(x => x.Country != null);
        RuleFor(x => x.BranchManagerName).MaximumLength(200).When(x => x.BranchManagerName != null);
    }
}

// ── UpsertCompanySettingsDto ───────────────────────────────────────────────

public class UpsertCompanySettingsDtoValidator : AbstractValidator<UpsertCompanySettingsDto>
{
    public UpsertCompanySettingsDtoValidator()
    {
        RuleFor(x => x.CompanyId)
            .GreaterThan(0).WithMessage("CompanyId must be a positive integer.");

        RuleFor(x => x.WorkingDaysPerMonth)
            .InclusiveBetween(1, 31).WithMessage("Working days per month must be between 1 and 31.");

        RuleFor(x => x.PFPercentage)
            .InclusiveBetween(0m, 20m).WithMessage("PF percentage must be between 0 and 20.");

        RuleFor(x => x.ESIPercentage)
            .InclusiveBetween(0m, 10m).WithMessage("ESI percentage must be between 0 and 10.");

        RuleFor(x => x.PTAmount)
            .InclusiveBetween(0m, 1000m).WithMessage("Professional tax amount must be between 0 and 1000.");

        RuleFor(x => x.PayslipFooterNote)
            .MaximumLength(500).When(x => x.PayslipFooterNote != null);

        RuleFor(x => x.OvertimeThresholdMinutes)
            .InclusiveBetween(1, 480).WithMessage("Overtime threshold must be between 1 and 480 minutes.")
            .When(x => x.OvertimeThresholdMinutes.HasValue);
    }
}
