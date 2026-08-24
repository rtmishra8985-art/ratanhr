using FluentValidation;
using HRMS.Application.DTOs.Training;

namespace HRMS.Application.Validators;

// ── CreateTrainingDto ──────────────────────────────────────────────────────────

public class CreateTrainingDtoValidator : AbstractValidator<CreateTrainingDto>
{
    public CreateTrainingDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Training title is required.")
            .MaximumLength(300).WithMessage("Training title must not exceed 300 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(3000).When(x => x.Description != null);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after the start date.");

        RuleFor(x => x.Trainer)
            .MaximumLength(200).When(x => x.Trainer != null);

        RuleFor(x => x.MaxSeats)
            .GreaterThan(0).WithMessage("Maximum seats must be at least 1.")
            .LessThanOrEqualTo(5000).WithMessage("Maximum seats cannot exceed 5000.");
    }
}

// ── EnrollDto ──────────────────────────────────────────────────────────────────

public class EnrollDtoValidator : AbstractValidator<EnrollDto>
{
    public EnrollDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");
    }
}

// ── MarkCompleteDto ────────────────────────────────────────────────────────────

public class MarkCompleteDtoValidator : AbstractValidator<MarkCompleteDto>
{
    public MarkCompleteDtoValidator()
    {
        RuleFor(x => x.CompletionDate)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Completion date cannot be in the future.")
            .When(x => x.CompletionDate.HasValue);

        RuleFor(x => x.CertificatePath)
            .MaximumLength(1000)
            .WithMessage("Certificate path must not exceed 1000 characters.")
            .When(x => x.CertificatePath != null);
    }
}
