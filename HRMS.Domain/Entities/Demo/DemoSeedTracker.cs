namespace HRMS.Domain.Entities.Demo;

/// <summary>
/// Tracks demo mode seed operations for idempotency.
/// Prevents duplicate seed runs and provides visibility into when demo data was created.
/// </summary>
public class DemoSeedTracker
{
    public int Id { get; set; }

    /// <summary>
    /// Semantic version of the demo seed operation (e.g., "1.0.0").
    /// Same version is never seeded twice — check before seeding to prevent duplicates.
    /// </summary>
    public string SeedVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Unique identifier for this seed run (Guid).
    /// Allows linking all demo records created in a single operation.
    /// </summary>
    public Guid SeedRunId { get; set; } = Guid.NewGuid();

    /// <summary>Count of demo companies created in this run.</summary>
    public int CreatedCompanyCount { get; set; }

    /// <summary>Count of demo employees created in this run.</summary>
    public int CreatedEmployeeCount { get; set; }

    /// <summary>Count of demo attendance records created in this run.</summary>
    public int CreatedAttendanceCount { get; set; }

    /// <summary>Count of demo leave requests created in this run.</summary>
    public int CreatedLeaveRequestCount { get; set; }

    /// <summary>Count of demo payslips created in this run.</summary>
    public int CreatedPayslipCount { get; set; }

    /// <summary>Count of demo bonuses created in this run.</summary>
    public int CreatedBonusCount { get; set; }

    /// <summary>Count of demo deductions created in this run.</summary>
    public int CreatedDeductionCount { get; set; }

    /// <summary>Count of demo recruitment candidates created in this run.</summary>
    public int CreatedCandidateCount { get; set; }

    /// <summary>Count of demo assets created in this run.</summary>
    public int CreatedAssetCount { get; set; }

    /// <summary>Count of demo users created in this run.</summary>
    public int CreatedUserCount { get; set; }

    /// <summary>Count of demo skills created in this run.</summary>
    public int CreatedSkillCount { get; set; }

    /// <summary>Count of demo project assignments created in this run.</summary>
    public int CreatedProjectAssignmentCount { get; set; }

    /// <summary>Count of demo awards created in this run.</summary>
    public int CreatedAwardCount { get; set; }

    /// <summary>Total records created across all entity types.</summary>
    public long TotalRecordsCreated => 
        CreatedCompanyCount + CreatedEmployeeCount + CreatedAttendanceCount +
        CreatedLeaveRequestCount + CreatedPayslipCount + CreatedBonusCount +
        CreatedDeductionCount + CreatedCandidateCount + CreatedAssetCount +
        CreatedUserCount + CreatedSkillCount + CreatedProjectAssignmentCount +
        CreatedAwardCount;

    /// <summary>When this seed operation was executed.</summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Environment where seed was executed (Development, Staging, Production).</summary>
    public string Environment { get; set; } = "Development";

    /// <summary>Whether the seed operation completed successfully.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Error message if seed failed, null if successful.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Free-form notes about this seed run.</summary>
    public string? Notes { get; set; }
}
