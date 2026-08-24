using FluentValidation;
using HRMS.Application.DTOs.Performance;

namespace HRMS.Application.Validators;

// ── CreateCycleDto ─────────────────────────────────────────────────────────────

public class CreateCycleDtoValidator : AbstractValidator<CreateCycleDto>
{
    private static readonly string[] ValidReviewTypes = { "Annual", "Semi-Annual", "Quarterly", "Monthly", "Probation" };

    public CreateCycleDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cycle name is required.")
            .MaximumLength(200).WithMessage("Cycle name must not exceed 200 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.ReviewType)
            .NotEmpty().WithMessage("Review type is required.")
            .Must(t => ValidReviewTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Review type must be one of: {string.Join(", ", ValidReviewTypes)}.");
    }
}

// ── UpdateCycleDto ─────────────────────────────────────────────────────────────

public class UpdateCycleDtoValidator : AbstractValidator<UpdateCycleDto>
{
    private static readonly string[] ValidReviewTypes = { "Annual", "Semi-Annual", "Quarterly", "Monthly", "Probation" };
    private static readonly string[] ValidStatuses    = { "Draft", "Active", "Closed" };

    public UpdateCycleDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Cycle name is required.")
            .MaximumLength(200).WithMessage("Cycle name must not exceed 200 characters.");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");

        RuleFor(x => x.ReviewType)
            .NotEmpty()
            .Must(t => ValidReviewTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Review type must be one of: {string.Join(", ", ValidReviewTypes)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.ReviewType));

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

// ── CreateGoalDto ──────────────────────────────────────────────────────────────

public class CreateGoalDtoValidator : AbstractValidator<CreateGoalDto>
{
    private static readonly string[] ValidGoalTypes  = { "Individual", "Team", "Department", "Company" };
    private static readonly string[] ValidCategories = { "Performance", "Learning", "Behavioral", "Operational" };

    public CreateGoalDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Goal title is required.")
            .MaximumLength(300).WithMessage("Goal title must not exceed 300 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Goal description is required.")
            .MaximumLength(2000).WithMessage("Goal description must not exceed 2000 characters.");

        RuleFor(x => x.GoalType)
            .NotEmpty()
            .Must(t => ValidGoalTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Goal type must be one of: {string.Join(", ", ValidGoalTypes)}.");

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(c => ValidCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Category must be one of: {string.Join(", ", ValidCategories)}.");

        RuleFor(x => x.TargetValue)
            .GreaterThan(0).WithMessage("Target value must be greater than zero.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(50);

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .WithMessage("Due date must be in the future.");

        RuleFor(x => x.Weight)
            .InclusiveBetween(1, 100)
            .WithMessage("Weight must be between 1 and 100.");
    }
}

// ── UpdateGoalProgressDto ──────────────────────────────────────────────────────

public class UpdateGoalProgressDtoValidator : AbstractValidator<UpdateGoalProgressDto>
{
    public UpdateGoalProgressDtoValidator()
    {
        RuleFor(x => x.AchievedValue)
            .GreaterThanOrEqualTo(0).WithMessage("Achieved value must be non-negative.");
    }
}

// ── CreateFeedbackDto ──────────────────────────────────────────────────────────

public class CreateFeedbackDtoValidator : AbstractValidator<CreateFeedbackDto>
{
    private static readonly string[] ValidFeedbackTypes = { "Praise", "Constructive", "Neutral", "360" };

    public CreateFeedbackDtoValidator()
    {
        RuleFor(x => x.ToEmployeeId)
            .NotEmpty().WithMessage("Recipient employee ID is required.");

        RuleFor(x => x.FeedbackText)
            .NotEmpty().WithMessage("Feedback text is required.")
            .MinimumLength(10).WithMessage("Feedback must be at least 10 characters.")
            .MaximumLength(3000).WithMessage("Feedback must not exceed 3000 characters.");

        RuleFor(x => x.FeedbackType)
            .NotEmpty()
            .Must(t => ValidFeedbackTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Feedback type must be one of: {string.Join(", ", ValidFeedbackTypes)}.");
    }
}

// ── CreateReviewDto ────────────────────────────────────────────────────────────

public class CreateReviewDtoValidator : AbstractValidator<CreateReviewDto>
{
    private static readonly string[] ValidReviewTypes = { "Annual", "Semi-Annual", "Quarterly", "Monthly", "Probation" };

    public CreateReviewDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.ReviewerId)
            .GreaterThan(0).WithMessage("Reviewer ID must be a positive integer.");

        RuleFor(x => x.ReviewType)
            .NotEmpty()
            .Must(t => ValidReviewTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Review type must be one of: {string.Join(", ", ValidReviewTypes)}.");
    }
}

// ── SubmitSelfReviewDto ────────────────────────────────────────────────────────

public class SubmitSelfReviewDtoValidator : AbstractValidator<SubmitSelfReviewDto>
{
    public SubmitSelfReviewDtoValidator()
    {
        RuleFor(x => x.SelfRating)
            .InclusiveBetween(1, 5).WithMessage("Self rating must be between 1 and 5.");

        RuleFor(x => x.SelfComments)
            .NotEmpty().WithMessage("Self comments are required.")
            .MaximumLength(3000);

        RuleFor(x => x.OverallComments)
            .MaximumLength(3000).When(x => x.OverallComments != null);
    }
}

// ── SubmitManagerReviewDto ─────────────────────────────────────────────────────

public class SubmitManagerReviewDtoValidator : AbstractValidator<SubmitManagerReviewDto>
{
    public SubmitManagerReviewDtoValidator()
    {
        RuleFor(x => x.ManagerRating)
            .InclusiveBetween(1, 5).WithMessage("Manager rating must be between 1 and 5.");

        RuleFor(x => x.ManagerComments)
            .NotEmpty().WithMessage("Manager comments are required.")
            .MaximumLength(3000);
    }
}

// ── FinalizeReviewDto ──────────────────────────────────────────────────────────

public class FinalizeReviewDtoValidator : AbstractValidator<FinalizeReviewDto>
{
    public FinalizeReviewDtoValidator()
    {
        RuleFor(x => x.FinalRating)
            .InclusiveBetween(1, 5).WithMessage("Final rating must be between 1 and 5.");

        RuleFor(x => x.HrComments)
            .MaximumLength(3000).When(x => x.HrComments != null);
    }
}
