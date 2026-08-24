using FluentValidation;
using HRMS.Application.DTOs.Expense;

namespace HRMS.Application.Validators;

// ── CreateExpenseClaimDto ──────────────────────────────────────────────────────

public class CreateExpenseClaimDtoValidator : AbstractValidator<CreateExpenseClaimDto>
{
    public CreateExpenseClaimDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Expense claim title is required.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be a valid 3-letter ISO code (e.g. INR, USD).")
            .Matches(@"^[A-Z]{3}$")
            .WithMessage("Currency must be an uppercase 3-letter ISO code.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes != null);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("An expense claim must contain at least one line item.")
            .Must(items => items.Count <= 50)
            .WithMessage("An expense claim cannot have more than 50 line items.");

        RuleForEach(x => x.Items).SetValidator(new CreateExpenseItemDtoValidator());
    }
}

// ── CreateExpenseItemDto ───────────────────────────────────────────────────────

public class CreateExpenseItemDtoValidator : AbstractValidator<CreateExpenseItemDto>
{
    private static readonly string[] ValidCategories =
        { "Hotel", "Flight", "Cab", "Fuel", "Food", "Train", "Bus", "Miscellaneous" };

    public CreateExpenseItemDtoValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Expense category is required.")
            .Must(c => ValidCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Category must be one of: {string.Join(", ", ValidCategories)}.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Expense item description is required.")
            .MaximumLength(500);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.GstAmount)
            .GreaterThanOrEqualTo(0).WithMessage("GST amount must be non-negative.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches(@"^[A-Z]{3}$")
            .WithMessage("Currency must be an uppercase 3-letter ISO code.");

        RuleFor(x => x.ExpenseDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Expense date cannot be in the future.");
    }
}

// ── ExpenseDecisionDto ─────────────────────────────────────────────────────────

public class ExpenseDecisionDtoValidator : AbstractValidator<ExpenseDecisionDto>
{
    public ExpenseDecisionDtoValidator()
    {
        RuleFor(x => x.Comments)
            .MaximumLength(1000).When(x => x.Comments != null);
    }
}
