namespace HRMS.Infrastructure.Services.Demo;

/// <summary>
/// Service for safely creating and managing demo data in RatanHR.
/// 
/// Responsibilities:
/// - Create 5 demo companies with realistic HRMS data
/// - Mark all demo records with IsDemo = true for isolation
/// - Support dry-run mode (preview without modifications)
/// - Support cleanup mode (delete only demo records)
/// - Enforce idempotency (same version never seeds twice)
/// - Validate safety preconditions before seeding
/// - Prevent accidental production data corruption
/// </summary>
public interface IDemoSeedService
{
    /// <summary>
    /// Executes the demo seed operation or shows a dry-run preview.
    /// </summary>
    /// <param name="dryRun">When true, preview without modifications. When false, actually create records.</param>
    /// <param name="verbose">When true, include detailed record counts and progress logging.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Result object with counts and status.</returns>
    Task<DemoSeedResult> SeedAsync(bool dryRun = true, bool verbose = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up all demo records (where IsDemo = true) from the database.
    /// Requires explicit confirmation.
    /// </summary>
    /// <param name="dryRun">When true, preview what would be deleted. When false, actually delete.</param>
    /// <param name="confirmCleanup">Must be explicitly true to proceed with actual cleanup (dryRun=false).</param>
    /// <param name="verbose">When true, include detailed record counts and progress logging.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Result object with counts and status.</returns>
    Task<DemoCleanupResult> CleanupAsync(
        bool dryRun = true,
        bool confirmCleanup = false,
        bool verbose = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates all preconditions for safe demo seeding.
    /// Returns detailed status of each check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Validation result with status of all checks.</returns>
    Task<DemoValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>Result of a demo seed operation.</summary>
public class DemoSeedResult
{
    public bool IsSuccess { get; set; }
    public bool WasDryRun { get; set; }
    public string Message { get; set; } = string.Empty;

    public int CompaniesCreated { get; set; }
    public int EmployeesCreated { get; set; }
    public int AttendanceRecordsCreated { get; set; }
    public int LeaveRequestsCreated { get; set; }
    public int PayslipsCreated { get; set; }
    public int BonusesCreated { get; set; }
    public int DeductionsCreated { get; set; }
    public int CandidatesCreated { get; set; }
    public int AssetsCreated { get; set; }
    public int UsersCreated { get; set; }
    public int SkillsCreated { get; set; }
    public int ProjectAssignmentsCreated { get; set; }
    public int AwardsCreated { get; set; }

    public long TotalRecordsCreated =>
        CompaniesCreated + EmployeesCreated + AttendanceRecordsCreated +
        LeaveRequestsCreated + PayslipsCreated + BonusesCreated +
        DeductionsCreated + CandidatesCreated + AssetsCreated +
        UsersCreated + SkillsCreated + ProjectAssignmentsCreated + AwardsCreated;

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

/// <summary>Result of a demo cleanup operation.</summary>
public class DemoCleanupResult
{
    public bool IsSuccess { get; set; }
    public bool WasDryRun { get; set; }
    public string Message { get; set; } = string.Empty;

    public int CompaniesDeleted { get; set; }
    public int EmployeesDeleted { get; set; }
    public int AttendanceRecordsDeleted { get; set; }
    public int LeaveRequestsDeleted { get; set; }
    public int PayslipsDeleted { get; set; }
    public int BonusesDeleted { get; set; }
    public int DeductionsDeleted { get; set; }
    public int CandidatesDeleted { get; set; }
    public int AssetsDeleted { get; set; }
    public int UsersDeleted { get; set; }
    public int SkillsDeleted { get; set; }
    public int ProjectAssignmentsDeleted { get; set; }
    public int AwardsDeleted { get; set; }

    public long TotalRecordsDeleted =>
        CompaniesDeleted + EmployeesDeleted + AttendanceRecordsDeleted +
        LeaveRequestsDeleted + PayslipsDeleted + BonusesDeleted +
        DeductionsDeleted + CandidatesDeleted + AssetsDeleted +
        UsersDeleted + SkillsDeleted + ProjectAssignmentsDeleted + AwardsDeleted;

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

/// <summary>Result of demo mode validation.</summary>
public class DemoValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationCheck> Checks { get; set; } = new();
    public List<string> FailureReasons { get; set; } = new();
}

/// <summary>Individual validation check result.</summary>
public class ValidationCheck
{
    public string CheckName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
}
