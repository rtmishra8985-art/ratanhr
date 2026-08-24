using FluentValidation;
using HRMS.Application.DTOs.Recruitment;

namespace HRMS.Application.Validators;

// ── CreateCandidateDto ─────────────────────────────────────────────────────────

public class CreateCandidateDtoValidator : AbstractValidator<CreateCandidateDto>
{
    private static readonly string[] ValidSourceChannels =
        { "Portal", "LinkedIn", "Referral", "Agency", "Walk-in", "Job Board", "Other" };

    public CreateCandidateDtoValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(255);

        RuleFor(x => x.Phone)
            .MaximumLength(20).When(x => x.Phone != null)
            .Matches(@"^[\d\s\+\-\(\)]+$")
            .WithMessage("Phone number contains invalid characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.TotalExperience)
            .InclusiveBetween(0, 60).WithMessage("Total experience must be between 0 and 60 years.");

        RuleFor(x => x.SourceChannel)
            .Must(s => ValidSourceChannels.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Source channel must be one of: {string.Join(", ", ValidSourceChannels)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.SourceChannel));

        RuleFor(x => x.Skills)
            .MaximumLength(2000).When(x => x.Skills != null);

        RuleFor(x => x.QualificationSummary)
            .MaximumLength(3000).When(x => x.QualificationSummary != null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes != null);
    }
}

// ── CreateRequisitionDto ───────────────────────────────────────────────────────

public class CreateRequisitionDtoValidator : AbstractValidator<CreateRequisitionDto>
{
    private static readonly string[] ValidJobTypes = { "Full-Time", "Part-Time", "Contract", "Internship", "Freelance" };

    public CreateRequisitionDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Job title is required.")
            .MaximumLength(200).WithMessage("Job title must not exceed 200 characters.");

        RuleFor(x => x.DepartmentName)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Job description is required.")
            .MaximumLength(5000);

        RuleFor(x => x.OpeningsCount)
            .GreaterThan(0).WithMessage("Number of openings must be at least 1.")
            .LessThanOrEqualTo(500).WithMessage("Number of openings cannot exceed 500.");

        RuleFor(x => x.JobType)
            .NotEmpty()
            .Must(t => ValidJobTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Job type must be one of: {string.Join(", ", ValidJobTypes)}.");

        RuleFor(x => x.MinSalary)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum salary must be non-negative.")
            .When(x => x.MinSalary.HasValue);

        RuleFor(x => x.MaxSalary)
            .GreaterThanOrEqualTo(x => x.MinSalary ?? 0)
            .WithMessage("Maximum salary must be greater than or equal to minimum salary.")
            .When(x => x.MaxSalary.HasValue);

        RuleFor(x => x.ClosingDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .WithMessage("Closing date must be in the future.")
            .When(x => x.ClosingDate.HasValue);
    }
}

// ── ScheduleInterviewDto ───────────────────────────────────────────────────────

public class ScheduleInterviewDtoValidator : AbstractValidator<ScheduleInterviewDto>
{
    private static readonly string[] ValidInterviewTypes =
        { "Phone Screen", "Video", "In-Person", "Technical", "HR", "Panel", "Assessment" };

    public ScheduleInterviewDtoValidator()
    {
        RuleFor(x => x.CandidateId)
            .GreaterThan(0).WithMessage("Candidate ID must be a positive integer.");

        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Interview must be scheduled in the future.");

        RuleFor(x => x.InterviewType)
            .NotEmpty().WithMessage("Interview type is required.")
            .Must(t => ValidInterviewTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Interview type must be one of: {string.Join(", ", ValidInterviewTypes)}.");

        RuleFor(x => x.Venue)
            .NotEmpty().WithMessage("Venue or meeting link is required.")
            .MaximumLength(500);

        RuleFor(x => x.InterviewerNames)
            .NotEmpty().WithMessage("Interviewer name(s) are required.")
            .MaximumLength(500);
    }
}

// ── SubmitFeedbackDto (Interview Feedback) ─────────────────────────────────────

public class SubmitInterviewFeedbackDtoValidator : AbstractValidator<SubmitFeedbackDto>
{
    private static readonly string[] ValidRecommendations = { "Strong Yes", "Yes", "Maybe", "No", "Strong No" };
    private static readonly string[] ValidStatuses        = { "Completed", "No Show", "Cancelled" };

    public SubmitInterviewFeedbackDtoValidator()
    {
        RuleFor(x => x.FeedbackScore)
            .InclusiveBetween(1, 10).WithMessage("Feedback score must be between 1 and 10.");

        RuleFor(x => x.FeedbackNotes)
            .NotEmpty().WithMessage("Feedback notes are required.")
            .MaximumLength(3000);

        RuleFor(x => x.Recommendation)
            .NotEmpty()
            .Must(r => ValidRecommendations.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Recommendation must be one of: {string.Join(", ", ValidRecommendations)}.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }
}

// ── CreateOfferDto ─────────────────────────────────────────────────────────────

public class CreateOfferDtoValidator : AbstractValidator<CreateOfferDto>
{
    public CreateOfferDtoValidator()
    {
        RuleFor(x => x.CandidateId)
            .GreaterThan(0).WithMessage("Candidate ID must be a positive integer.");

        RuleFor(x => x.OfferedDesignation)
            .NotEmpty().WithMessage("Offered designation is required.")
            .MaximumLength(200);

        RuleFor(x => x.OfferedDepartment)
            .NotEmpty().WithMessage("Offered department is required.")
            .MaximumLength(200);

        RuleFor(x => x.OfferedSalary)
            .GreaterThan(0).WithMessage("Offered salary must be greater than zero.");

        RuleFor(x => x.JoiningDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .WithMessage("Joining date must be in the future.");

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(x => x.JoiningDate)
            .WithMessage("Offer expiry date must be after the joining date.");
    }
}
