using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Attendance;

public class WebAttendance : ICompanyOwned
{
    public int       Id         { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int AttendanceId { get => Id; set => Id = value; }
    public string    EmployeeId { get; set; } = string.Empty;
    /// <summary>
    /// Tenant discriminator. Null = legacy record created before multi-tenancy was enforced.
    /// </summary>
    public int?      CompanyId  { get; set; }
    public DateOnly  AttDate    { get; set; }
    /// <summary>Alias for AttDate — tests and AutoMapper use Date.</summary>
    [NotMapped] public DateOnly Date { get => AttDate; set => AttDate = value; }
    /// <summary>Time of day the employee checked in. Maps to MySQL TIME column.</summary>
    public TimeOnly? CheckIn    { get; set; }
    /// <summary>Time of day the employee checked out. Maps to MySQL TIME column.</summary>
    public TimeOnly? CheckOut   { get; set; }
    public string    Status     { get; set; } = "Present"; // Present | Half Day | Absent | Leave | Holiday | Weekend
    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Populated by HR/Admin when editing a past attendance record.
    /// </summary>
    public string? AdminEditReason { get; set; }

    /// <summary>
    /// Alias for AdminEditReason used by edit flows and unit tests.
    /// Not persisted separately — maps to the same column.
    /// </summary>
    [NotMapped] public string? Reason { get => AdminEditReason; set => AdminEditReason = value; }

    /// <summary>
    /// Minutes worked beyond the standard 9-hour day.
    /// Calculated and stored on check-out so reporting queries never
    /// have to recompute it from raw check-in/check-out timestamps.
    /// Zero for non-overtime days.
    /// </summary>
    public int OvertimeMinutes { get; set; }

    // ── Soft delete ───────────────────────────────────────────────────────────
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedReason { get; set; }

    /// <summary>
    /// True when this record was created by the demo-mode seed service
    /// (<see cref="HRMS.Infrastructure.Services.Demo.DemoSeedService"/>). Used by
    /// CleanupAsync to delete only demo data and never touch real customer records.
    /// </summary>
    public bool IsDemo { get; set; } = false;
}
