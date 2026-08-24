using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Onboarding;

public class OnboardingTemplate : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional longer heading for display in the UI (added in Phase 2).
    /// Falls back to Name when null.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Backward-compatible plain-text description for legacy records that
    /// pre-date the JSON step format. Null for all new records.
    /// When Steps is empty ("[]") and this is set, the API exposes this value
    /// as the human-readable summary.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>JSON array of step objects: [{title, description, order}]</summary>
    public string Steps { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Computed helper ──────────────────────────────────────────────────────
    /// <summary>
    /// Returns the effective display title: Title ?? Name.
    /// </summary>
    public string DisplayTitle => Title ?? Name;

    /// <summary>
    /// Returns true when this template uses the legacy description field
    /// rather than the structured JSON steps array.
    /// </summary>
    public bool IsLegacyFormat =>
        !string.IsNullOrWhiteSpace(Description) &&
        (string.IsNullOrWhiteSpace(Steps) || Steps.Trim() == "[]");
}
