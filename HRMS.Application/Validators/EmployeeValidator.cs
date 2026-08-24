using FluentValidation;
using HRMS.Application.DTOs.Employee;

namespace HRMS.Application.Validators;

// ── CreateEmployeeDto ──────────────────────────────────────────────────────

public class CreateEmployeeDtoValidator : AbstractValidator<CreateEmployeeDto>
{
    private static readonly string[] ValidGenders   = { "Male", "Female", "Other" };
    private static readonly string[] ValidMarital   = { "Single", "Married", "Divorced", "Widowed" };
    private static readonly string[] ValidBloodGrp  = { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };

    public CreateEmployeeDtoValidator()
    {
        // ── Required employment fields ─────────────────────────────────────
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(255).WithMessage("Full name must not exceed 255 characters.");

        RuleFor(x => x.Designation)
            .NotEmpty().WithMessage("Designation is required.")
            .MaximumLength(200).WithMessage("Designation must not exceed 200 characters.");

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Department is required.")
            .MaximumLength(200).WithMessage("Department must not exceed 200 characters.");

        // ── Personal — optional but validated when provided ────────────────
        RuleFor(x => x.Gender)
            .Must(g => ValidGenders.Contains(g, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Gender must be one of: {string.Join(", ", ValidGenders)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Gender));

        RuleFor(x => x.MaritalStatus)
            .Must(m => ValidMarital.Contains(m, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Marital status must be one of: {string.Join(", ", ValidMarital)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.MaritalStatus));

        RuleFor(x => x.BloodGroup)
            .Must(b => ValidBloodGrp.Contains(b))
            .WithMessage($"Blood group must be one of: {string.Join(", ", ValidBloodGrp)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.BloodGroup));

        // Date of Birth: valid date, not in future, reasonable lower bound (1900)
        RuleFor(x => x.Dob)
            .Must(BeValidDate).WithMessage("Date of birth must be a valid date (yyyy-MM-dd).")
            .Must(d => DateOnly.TryParse(d, out var dt) && dt <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Date of birth cannot be in the future.")
            .Must(d => DateOnly.TryParse(d, out var dt) && dt.Year >= 1900)
            .WithMessage("Date of birth year must be 1900 or later.")
            .When(x => !string.IsNullOrWhiteSpace(x.Dob));

        // Date of Joining: valid date, not more than 50 years in past
        RuleFor(x => x.Doj)
            .Must(BeValidDate).WithMessage("Date of joining must be a valid date (yyyy-MM-dd).")
            .Must(d => DateOnly.TryParse(d, out var dt) && dt.Year >= 1980)
            .WithMessage("Date of joining year must be 1980 or later.")
            .When(x => !string.IsNullOrWhiteSpace(x.Doj));

        // Aadhaar: exactly 12 digits
        RuleFor(x => x.Aadhaar)
            .Matches(@"^\d{12}$").WithMessage("Aadhaar must be exactly 12 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.Aadhaar));

        // PAN: 10 characters, format AAAAA9999A
        RuleFor(x => x.Pan)
            .Length(10).WithMessage("PAN must be exactly 10 characters.")
            .Matches(@"^[A-Z]{5}[0-9]{4}[A-Z]$").WithMessage("PAN format is invalid (expected: AAAAA9999A).")
            .When(x => !string.IsNullOrWhiteSpace(x.Pan));

        // ── Bank / payroll details ────────────────────────────────────────
        // FIX (MED-VAL): Added missing financial-field validations.

        // IFSC code: 4 uppercase letters + '0' + 6 alphanumeric chars (RBI format)
        RuleFor(x => x.IfscCode)
            .Matches(@"^[A-Z]{4}0[A-Z0-9]{6}$")
            .WithMessage("IFSC code format is invalid (expected: ABCD0123456).")
            .When(x => !string.IsNullOrWhiteSpace(x.IfscCode));

        // Bank account number: 9–18 digits (covers all Indian bank ranges)
        RuleFor(x => x.AccountNumber)
            .Matches(@"^\d{9,18}$")
            .WithMessage("Bank account number must be between 9 and 18 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.AccountNumber));

        // UAN: exactly 12 digits (EPFO Universal Account Number)
        RuleFor(x => x.Uan)
            .Matches(@"^\d{12}$")
            .WithMessage("UAN must be exactly 12 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.Uan));

        // Address length caps — match DB column text limits
        RuleFor(x => x.PermanentAddress)
            .MaximumLength(1000)
            .WithMessage("Permanent address must not exceed 1000 characters.")
            .When(x => x.PermanentAddress != null);

        RuleFor(x => x.CurrentAddress)
            .MaximumLength(1000)
            .WithMessage("Current address must not exceed 1000 characters.")
            .When(x => x.CurrentAddress != null);

        // ── String length limits ──────────────────────────────────────────
        RuleFor(x => x.Nationality).MaximumLength(100).When(x => x.Nationality != null);
        RuleFor(x => x.EmergencyContactName).MaximumLength(200).When(x => x.EmergencyContactName != null);
        RuleFor(x => x.EmergencyContactRelationship).MaximumLength(100).When(x => x.EmergencyContactRelationship != null);
        RuleFor(x => x.EmergencyContactPhone)
            .MaximumLength(20).When(x => x.EmergencyContactPhone != null)
            .Matches(@"^\+?[\d\s\-\(\)]{7,20}$")
            .WithMessage("Emergency contact phone must be a valid phone number.")
            .When(x => !string.IsNullOrWhiteSpace(x.EmergencyContactPhone));

        // ── Education ─────────────────────────────────────────────────────
        RuleFor(x => x.Qualification).MaximumLength(200).When(x => x.Qualification != null);
        RuleFor(x => x.Institution).MaximumLength(200).When(x => x.Institution != null);
        RuleFor(x => x.Specialization).MaximumLength(200).When(x => x.Specialization != null);
        RuleFor(x => x.YearOfPassing)
            .InclusiveBetween(1950, DateTime.Today.Year + 5)
            .WithMessage($"Year of passing must be between 1950 and {DateTime.Today.Year + 5}.")
            .When(x => x.YearOfPassing.HasValue);
    }

    private static bool BeValidDate(string? value)
        => string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, out _);
}
