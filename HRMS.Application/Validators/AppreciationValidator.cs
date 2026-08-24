using FluentValidation;
using HRMS.Application.DTOs.Appreciation;

namespace HRMS.Application.Validators;

// ── UploadAppreciationDto ──────────────────────────────────────────────────
// FIX 6: Missing FluentValidation validator for Appreciation DTOs.

public class UploadAppreciationDtoValidator : AbstractValidator<UploadAppreciationDto>
{
    // Audit item 9 — narrowed from { .pdf, .doc, .docx, images } to images only so
    // this list agrees with UploadProfile.Image, which both the controller gate and
    // AppreciationService/FileStorageService enforce. Previously a .pdf passed this
    // validator and was then rejected deeper in the stack with a different message.
    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public UploadAppreciationDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("EmployeeId is required.")
            .MaximumLength(20).WithMessage("EmployeeId must not exceed 20 characters.")
            .Matches(@"^[A-Za-z0-9\-_]+$")
            .WithMessage("EmployeeId must contain only alphanumeric characters, hyphens, or underscores.");

        RuleFor(x => x.Message)
            .MaximumLength(2000).When(x => x.Message != null)
            .WithMessage("Appreciation message must not exceed 2000 characters.");

        // File is optional — an appreciation can be text-only.
        RuleFor(x => x.FileSize)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .When(x => x.FileSize.HasValue)
            .WithMessage($"Appreciation file must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");

        RuleFor(x => x.FileExtension)
            .Must(ext => AllowedExtensions.Contains(ext?.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.FileExtension))
            .WithMessage($"Allowed file types: {string.Join(", ", AllowedExtensions)}.");
    }
}
