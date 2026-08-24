using FluentValidation;
using HRMS.Application.DTOs.Attendance;

namespace HRMS.Application.Validators;

// ── CreateShiftDto ─────────────────────────────────────────────────────────

public class CreateShiftDtoValidator : AbstractValidator<CreateShiftDto>
{
    public CreateShiftDtoValidator()
    {
        RuleFor(x => x.CompanyId)
            .GreaterThan(0).WithMessage("CompanyId must be a positive integer.");

        // Validate via the Name alias so tests using x.Name work correctly.
        // Name and ShiftName share the same backing field.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Shift name is required.")
            .MaximumLength(100).WithMessage("Shift name must not exceed 100 characters.");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Start time is required.")
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("Start time must be in HH:mm format (e.g. 09:00).");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("End time is required.")
            .Matches(@"^([01]\d|2[0-3]):[0-5]\d$")
            .WithMessage("End time must be in HH:mm format (e.g. 18:00).");

        RuleFor(x => x.GracePeriodMinutes)
            .InclusiveBetween(0, 60)
            .WithMessage("Grace period must be between 0 and 60 minutes.");
    }
}

/// <summary>Alias used by tests (CreateShiftValidator) — identical to CreateShiftDtoValidator.</summary>
public class CreateShiftValidator : CreateShiftDtoValidator { }

// ── UpdateAttendanceStatusDto ──────────────────────────────────────────────

public class UpdateAttendanceStatusDtoValidator : AbstractValidator<UpdateAttendanceStatusDto>
{
    private static readonly string[] ValidStatuses =
        { "Present", "Absent", "Half Day", "Leave", "Holiday", "Weekend" };

    public UpdateAttendanceStatusDtoValidator()
    {
        RuleFor(x => x.AttendanceId)
            .GreaterThan(0).WithMessage("AttendanceId must be a positive integer.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.");
    }
}

// ── EditAttendanceDto ──────────────────────────────────────────────────────
// Used by HR/Admin to back-date edit attendance with a mandatory reason.

public class EditAttendanceDtoValidator : AbstractValidator<EditAttendanceDto>
{
    private static readonly string[] ValidStatuses =
        { "Present", "Absent", "Half Day", "Leave", "Holiday", "Weekend" };

    public EditAttendanceDtoValidator()
    {
        RuleFor(x => x.AttendanceId)
            .GreaterThan(0).WithMessage("AttendanceId must be a positive integer.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.");

        // Reason is mandatory for admin back-dated edits — at least 10 chars for meaningful audit
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required for attendance edits.")
            .MinimumLength(10).WithMessage("Reason must be at least 10 characters.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

/// <summary>Alias used by tests (UpdateAttendanceStatusValidator) — identical to UpdateAttendanceStatusDtoValidator.</summary>
public class UpdateAttendanceStatusValidator : UpdateAttendanceStatusDtoValidator { }

/// <summary>Alias used by tests (EditAttendanceValidator) — identical to EditAttendanceDtoValidator.</summary>
public class EditAttendanceValidator : EditAttendanceDtoValidator { }
