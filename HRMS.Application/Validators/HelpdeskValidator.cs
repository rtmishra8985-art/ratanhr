using FluentValidation;
using HRMS.Application.DTOs.Helpdesk;

namespace HRMS.Application.Validators;

// ── CreateTicketDto ────────────────────────────────────────────────────────

/// <summary>
/// FIX GAP-HD-01: FluentValidation for helpdesk ticket creation.
/// Enforces title length, valid priority values, and optional category/description constraints.
/// Previously Helpdesk DTOs relied solely on [Required]/[StringLength] data annotations;
/// FluentValidation provides richer error messages, consistent API response formatting,
/// and parity with all other module validators.
/// </summary>
public class CreateTicketDtoValidator : AbstractValidator<CreateTicketDto>
{
    private static readonly string[] ValidPriorities = { "Low", "Medium", "High", "Critical" };

    public CreateTicketDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Ticket title is required.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.")
            .MinimumLength(5).WithMessage("Title must be at least 5 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5 000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(p => ValidPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("CategoryId must be a positive integer.")
            .When(x => x.CategoryId.HasValue);
    }
}

// ── UpdateTicketDto ────────────────────────────────────────────────────────

public class UpdateTicketDtoValidator : AbstractValidator<UpdateTicketDto>
{
    private static readonly string[] ValidStatuses   = { "Open", "In Progress", "Resolved", "Closed", "Cancelled" };
    private static readonly string[] ValidPriorities = { "Low", "Medium", "High", "Critical" };

    public UpdateTicketDtoValidator()
    {
        RuleFor(x => x.Title)
            .MinimumLength(5).WithMessage("Title must be at least 5 characters.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.")
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(5000).WithMessage("Description must not exceed 5 000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: Open, In Progress, Resolved, Closed, Cancelled.")
            .When(x => x.Status is not null);

        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Priority must be one of: Low, Medium, High, Critical.")
            .When(x => x.Priority is not null);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("CategoryId must be a positive integer.")
            .When(x => x.CategoryId.HasValue);
    }
}

// ── AssignTicketDto ────────────────────────────────────────────────────────

public class AssignTicketDtoValidator : AbstractValidator<AssignTicketDto>
{
    public AssignTicketDtoValidator()
    {
        RuleFor(x => x.AssignedToId)
            .NotEmpty().WithMessage("AssignedToId is required.")
            .MaximumLength(450).WithMessage("AssignedToId must not exceed 450 characters.");
    }
}

// ── CreateTicketCommentDto ─────────────────────────────────────────────────

public class CreateTicketCommentDtoValidator : AbstractValidator<CreateTicketCommentDto>
{
    public CreateTicketCommentDtoValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Comment message is required.")
            .MinimumLength(2).WithMessage("Comment must be at least 2 characters.")
            .MaximumLength(5000).WithMessage("Comment must not exceed 5 000 characters.");
    }
}

// ── CreateTicketCategoryDto ────────────────────────────────────────────────

public class CreateTicketCategoryDtoValidator : AbstractValidator<CreateTicketCategoryDto>
{
    public CreateTicketCategoryDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MinimumLength(2).WithMessage("Category name must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Category description must not exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}

// ── TicketQueryDto ─────────────────────────────────────────────────────────

public class TicketQueryDtoValidator : AbstractValidator<TicketQueryDto>
{
    private static readonly string[] ValidStatuses    = { "Open", "In Progress", "Resolved", "Closed", "Cancelled" };
    private static readonly string[] ValidPriorities  = { "Low", "Medium", "High", "Critical" };
    private static readonly string[] ValidSortFields  = { "createdat", "updatedat", "status", "priority", "title" };
    private static readonly string[] ValidSortDirs    = { "asc", "desc" };

    public TicketQueryDtoValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200).WithMessage("PageSize must be between 1 and 200.");

        RuleFor(x => x.Search)
            .MaximumLength(200).WithMessage("Search term must not exceed 200 characters.")
            .When(x => x.Search is not null);

        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status filter must be one of: Open, In Progress, Resolved, Closed, Cancelled.")
            .When(x => x.Status is not null);

        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Priority filter must be one of: Low, Medium, High, Critical.")
            .When(x => x.Priority is not null);

        RuleFor(x => x.SortBy)
            .Must(s => ValidSortFields.Contains(s?.ToLowerInvariant()))
            .WithMessage("SortBy must be one of: createdAt, updatedAt, status, priority, title.")
            .When(x => x.SortBy is not null);

        RuleFor(x => x.SortDirection)
            .Must(d => ValidSortDirs.Contains(d?.ToLowerInvariant()))
            .WithMessage("SortDirection must be 'asc' or 'desc'.")
            .When(x => x.SortDirection is not null);
    }
}
