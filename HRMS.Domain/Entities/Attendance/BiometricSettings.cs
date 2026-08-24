using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// Per-company biometric configuration settings.
/// One row per company. Created on first save; upserted thereafter.
/// </summary>
public class BiometricSettings : ICompanyOwned
{
    public int  Id        { get; set; }
    public int? CompanyId { get; set; }

    // ── Polling ───────────────────────────────────────────────────────────

    /// <summary>Whether automatic background polling is enabled for this company.</summary>
    public bool AutoSyncEnabled { get; set; } = false;

    /// <summary>How often (in minutes) the background service polls devices. Default: 30.</summary>
    public int SyncIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Number of past days to include in each polling window.
    /// Prevents gaps if the service was offline. Default: 1.
    /// </summary>
    public int SyncLookbackDays { get; set; } = 1;

    // ── Attendance Rules ──────────────────────────────────────────────────

    /// <summary>Grace time in minutes before a punch is marked Late. Default: 15.</summary>
    public int GraceTimeMinutes { get; set; } = 15;

    /// <summary>Minimum hours between two punches to treat them as distinct Check-In/Check-Out pair.</summary>
    public int MinHalfDayHours { get; set; } = 4;

    /// <summary>Whether duplicate punch detection is active (suppress punches within DedupeWindowMinutes).</summary>
    public bool EnableDuplicatePunchDetection { get; set; } = true;

    /// <summary>Time window in minutes within which a second punch from the same user is treated as duplicate.</summary>
    public int DedupeWindowMinutes { get; set; } = 5;

    /// <summary>Whether unrecognised device user IDs are queued for manual review (true) or silently skipped (false).</summary>
    public bool QueueUnknownEmployees { get; set; } = true;

    // ── Realtime ──────────────────────────────────────────────────────────

    /// <summary>Whether realtime push/webhook listening is active.</summary>
    public bool RealtimeEnabled { get; set; } = false;

    // ── Audit ─────────────────────────────────────────────────────────────

    /// <summary>Whether raw punch logs should be persisted to BiometricLogs table. Default: true.</summary>
    public bool PersistRawLogs { get; set; } = true;

    /// <summary>Number of days to retain raw BiometricLog rows. 0 = keep forever.</summary>
    public int LogRetentionDays { get; set; } = 90;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
