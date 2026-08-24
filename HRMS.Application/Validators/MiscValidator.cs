using FluentValidation;
using HRMS.Application.DTOs.Department;
using HRMS.Application.DTOs.Holiday;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Notification;

namespace HRMS.Application.Validators;

// ── CreateHolidayDto ───────────────────────────────────────────────────────

public class CreateHolidayDtoValidator : AbstractValidator<CreateHolidayDto>
{
    public CreateHolidayDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Holiday name is required.")
            .MaximumLength(200).WithMessage("Holiday name must not exceed 200 characters.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Holiday date is required.")
            .Must(d => DateOnly.TryParse(d, out _))
            .WithMessage("Holiday date must be a valid date in yyyy-MM-dd format.")
            // Holidays can be declared 3 years in advance or up to 1 year retroactively
            .Must(d => DateOnly.TryParse(d, out var dt)
                    && dt >= DateOnly.FromDateTime(DateTime.Today.AddYears(-1))
                    && dt <= DateOnly.FromDateTime(DateTime.Today.AddYears(3)))
            .WithMessage("Holiday date must be within 1 year past or 3 years future.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null);
    }
}

// ── CreateDepartmentDto ────────────────────────────────────────────────────

public class CreateDepartmentDtoValidator : AbstractValidator<CreateDepartmentDto>
{
    public CreateDepartmentDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(200).WithMessage("Department name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null);
    }
}

// ── CreateDesignationDto ───────────────────────────────────────────────────

public class CreateDesignationDtoValidator : AbstractValidator<CreateDesignationDto>
{
    public CreateDesignationDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Designation name is required.")
            .MaximumLength(200).WithMessage("Designation name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null);
    }
}

// ── CreateRoleDto ──────────────────────────────────────────────────────────

public class CreateRoleDtoValidator : AbstractValidator<CreateRoleDto>
{
    public CreateRoleDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(50).WithMessage("Role name must not exceed 50 characters.")
            .Matches(@"^[a-zA-Z0-9_\-\s]+$")
            .WithMessage("Role name may only contain letters, digits, spaces, hyphens, and underscores.");

        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null);
    }
}

// ── CreateNotificationDto ──────────────────────────────────────────────────

public class CreateNotificationDtoValidator : AbstractValidator<CreateNotificationDto>
{
    private static readonly string[] ValidTypes = { "info", "warning", "error", "success" };

    public CreateNotificationDtoValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId must be a positive integer.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Notification title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Notification message is required.")
            .MaximumLength(1000).WithMessage("Message must not exceed 1000 characters.");

        RuleFor(x => x.Type)
            .Must(t => ValidTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Type must be one of: info, warning, error, success.")
            .When(x => !string.IsNullOrWhiteSpace(x.Type));

        RuleFor(x => x.EntityType)
            .MaximumLength(100).When(x => x.EntityType != null);

        RuleFor(x => x.EntityId)
            .MaximumLength(50).When(x => x.EntityId != null);
    }
}
