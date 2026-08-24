using FluentValidation;
using HRMS.Application.DTOs.Employee;

namespace HRMS.Application.Validators;

// ── CreateTransferDto ──────────────────────────────────────────────────────

public class CreateTransferDtoValidator : AbstractValidator<CreateTransferDto>
{
    public CreateTransferDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.EffectiveDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(-5)))
            .WithMessage("Effective date must not be more than 5 years in the past.")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(2)))
            .WithMessage("Effective date must not be more than 2 years in the future.");

        RuleFor(x => x.ToDepartment)
            .MaximumLength(200).When(x => x.ToDepartment != null);

        RuleFor(x => x.ToDesignation)
            .MaximumLength(200).When(x => x.ToDesignation != null);

        RuleFor(x => x.Reason)
            .MaximumLength(500).When(x => x.Reason != null);

        RuleFor(x => x.ToCompanyId)
            .GreaterThan(0).WithMessage("ToCompanyId must be a positive integer.")
            .When(x => x.ToCompanyId.HasValue);

        RuleFor(x => x.ToBranchId)
            .GreaterThan(0).WithMessage("ToBranchId must be a positive integer.")
            .When(x => x.ToBranchId.HasValue);
    }
}

// ── CreatePromotionDto ─────────────────────────────────────────────────────

public class CreatePromotionDtoValidator : AbstractValidator<CreatePromotionDto>
{
    public CreatePromotionDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.ToDesignation)
            .MaximumLength(200).When(x => x.ToDesignation != null);

        RuleFor(x => x.ToDepartment)
            .MaximumLength(200).When(x => x.ToDepartment != null);

        RuleFor(x => x.SalaryIncrement)
            .GreaterThanOrEqualTo(0m).WithMessage("Salary increment must not be negative.")
            .When(x => x.SalaryIncrement.HasValue);

        RuleFor(x => x.EffectiveDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(-5)))
            .WithMessage("Effective date must not be more than 5 years in the past.")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(2)))
            .WithMessage("Effective date must not be more than 2 years in the future.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).When(x => x.Reason != null);

        RuleFor(x => x.Remarks)
            .MaximumLength(1000).When(x => x.Remarks != null);

        RuleFor(x => x.CreatedByUserId)
            .GreaterThan(0).WithMessage("CreatedByUserId must be a positive integer.");
    }
}

// ── InitiateExitDto ────────────────────────────────────────────────────────

public class InitiateExitDtoValidator : AbstractValidator<InitiateExitDto>
{
    private static readonly string[] ValidExitTypes =
        { "Resignation", "Termination", "Retirement", "Absconding", "EndOfContract" };

    public InitiateExitDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.ExitType)
            .NotEmpty().WithMessage("Exit type is required.")
            .Must(t => ValidExitTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Exit type must be one of: {string.Join(", ", ValidExitTypes)}.");

        RuleFor(x => x.ResignationDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddYears(-1)))
            .WithMessage("Resignation date must not be more than 1 year in the past.")
            .When(x => x.ResignationDate.HasValue);

        RuleFor(x => x.LastWorkingDate)
            .GreaterThanOrEqualTo(x => x.ResignationDate ?? DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Last working date must be on or after the resignation date.")
            .When(x => x.LastWorkingDate.HasValue && x.ResignationDate.HasValue);

        RuleFor(x => x.Reason)
            .MaximumLength(1000).When(x => x.Reason != null);

        RuleFor(x => x.InitiatedByUserId)
            .GreaterThan(0).WithMessage("InitiatedByUserId must be a positive integer.");
    }
}

// ── CompleteExitDto ────────────────────────────────────────────────────────

public class CompleteExitDtoValidator : AbstractValidator<CompleteExitDto>
{
    public CompleteExitDtoValidator()
    {
        RuleFor(x => x.GratuityAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Gratuity amount must not be negative.")
            .When(x => x.GratuityAmount.HasValue);

        RuleFor(x => x.SettlementAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Settlement amount must not be negative.")
            .When(x => x.SettlementAmount.HasValue);

        RuleFor(x => x.InterviewNotes)
            .MaximumLength(2000).When(x => x.InterviewNotes != null);
    }
}

// ── UploadDocumentDto ──────────────────────────────────────────────────────

public class UploadDocumentDtoValidator : AbstractValidator<UploadDocumentDto>
{
    private static readonly string[] ValidDocumentTypes =
    {
        "Aadhaar", "PAN", "Passport", "DrivingLicense", "VoterID",
        "EducationCertificate", "ExperienceLetter", "OfferLetter",
        "RelievingLetter", "SalarySlip", "BankStatement", "Other"
    };

    public UploadDocumentDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.");

        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("Document type is required.")
            .MaximumLength(100).WithMessage("Document type must not exceed 100 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes != null);
    }
}
