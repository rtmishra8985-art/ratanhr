using FluentValidation;
using HRMS.Application.DTOs.Auth;

namespace HRMS.Application.Validators;

// ── LoginDto ───────────────────────────────────────────────────────────────

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    private static readonly string[] ValidPortals = { "employee", "admin", "superadmin" };

    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.Portal)
            .NotEmpty().WithMessage("Portal is required.")
            .Must(p => ValidPortals.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Portal must be one of: employee, admin, superadmin.");
    }
}

// ── ForgotPasswordDto ──────────────────────────────────────────────────────

public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(255);
    }
}

// ── ResetPasswordDto ───────────────────────────────────────────────────────

public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

// ── ChangePasswordDto ──────────────────────────────────────────────────────

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from current password.");
    }
}

// ── UpdateProfileDto ───────────────────────────────────────────────────────

public class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(x => x.FullName)
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters.")
            .When(x => x.FullName != null);
    }
}

/// <summary>Alias used by tests (LoginValidator) — identical to LoginDtoValidator.</summary>
public class LoginValidator : LoginDtoValidator { }

/// <summary>Alias used by tests (ResetPasswordValidator) — identical to ResetPasswordDtoValidator.</summary>
public class ResetPasswordValidator : ResetPasswordDtoValidator { }
