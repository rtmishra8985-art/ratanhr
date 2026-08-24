namespace HRMS.Infrastructure.Options;

/// <summary>
/// Configuration options for Demo Mode operation.
/// Bound from appsettings.json [DemoMode] section.
/// </summary>
public class DemoModeOptions
{
    public const string SectionName = "DemoMode";

    /// <summary>
    /// When true, demo seeding is enabled. Default: false.
    /// Must be explicitly set in configuration to allow seeding.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When true, the actual seed/cleanup operations can run.
    /// When false, only dry-run is allowed. Default: false.
    /// </summary>
    public bool SeedEnabled { get; set; } = false;

    /// <summary>
    /// When false (default), demo seeding is blocked in Production environment.
    /// When true, production seeding is allowed (but still requires SeedEnabled).
    /// </summary>
    public bool AllowProduction { get; set; } = false;

    /// <summary>
    /// Semantic version of the demo seed operation (e.g., "1.0.0").
    /// Used for idempotency — same version is never seeded twice.
    /// </summary>
    public string SeedVersion { get; set; } = "1.0.0";

    /// <summary>
    /// When true, dry-run mode is the default. Operations must explicitly request actual execution.
    /// When false, actual execution is the default (dangerous — not recommended).
    /// </summary>
    public bool DryRunByDefault { get; set; } = true;

    /// <summary>
    /// IDs reserved for demo companies (1-5). Real customer companies start at 1000.
    /// </summary>
    public static readonly int[] ReservedDemoCompanyIds = { 1, 2, 3, 4, 5 };
}
