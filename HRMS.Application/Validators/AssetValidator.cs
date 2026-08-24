using FluentValidation;
using HRMS.Application.DTOs.Assets;

namespace HRMS.Application.Validators;

// ── CreateAssetDto ─────────────────────────────────────────────────────────────

public class CreateAssetDtoValidator : AbstractValidator<CreateAssetDto>
{
    public CreateAssetDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Asset name is required.")
            .MaximumLength(200).WithMessage("Asset name must not exceed 200 characters.");

        RuleFor(x => x.AssetCode)
            .NotEmpty().WithMessage("Asset code is required.")
            .MaximumLength(50).WithMessage("Asset code must not exceed 50 characters.")
            .Matches(@"^[A-Za-z0-9\-_]+$")
            .WithMessage("Asset code may only contain letters, digits, hyphens, and underscores.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).When(x => x.Description != null);

        RuleFor(x => x.SerialNumber)
            .MaximumLength(100).When(x => x.SerialNumber != null);

        RuleFor(x => x.PurchaseDate)
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("Purchase date cannot be in the future.")
            .When(x => x.PurchaseDate.HasValue);

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Purchase price must be non-negative.")
            .When(x => x.PurchasePrice.HasValue);

        RuleFor(x => x.Location)
            .MaximumLength(200).When(x => x.Location != null);
    }
}

// ── UpdateAssetDto ─────────────────────────────────────────────────────────────

public class UpdateAssetDtoValidator : AbstractValidator<UpdateAssetDto>
{
    private static readonly string[] ValidStatuses =
        { "Available", "Assigned", "Under Maintenance", "Lost", "Damaged", "Retired" };

    public UpdateAssetDtoValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).When(x => x.Name != null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).When(x => x.Description != null);

        RuleFor(x => x.Location)
            .MaximumLength(200).When(x => x.Location != null);

        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", ValidStatuses)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

// ── AssignAssetDto ─────────────────────────────────────────────────────────────

public class AssignAssetDtoValidator : AbstractValidator<AssignAssetDto>
{
    public AssignAssetDtoValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee ID is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => x.Notes != null);
    }
}
