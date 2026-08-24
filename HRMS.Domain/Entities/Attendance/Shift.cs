using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Attendance;

public class Shift : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string Name { get; set; } = string.Empty;
    public string ShiftName { get => Name; set => Name = value; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>Minutes after shift start before an employee is considered Late (on top of GracePeriod).</summary>
    public int GracePeriodMinutes { get; set; } = 15;

    /// <summary>
    /// Additional minutes of tolerance beyond the grace period before the attendance
    /// status changes from Late to Half Day. Set to 0 to treat any late arrival as Late.
    /// </summary>
    public int LateThresholdMinutes { get; set; } = 0;

    /// <summary>
    /// Minimum hours worked to be counted as Half Day (not Absent).
    /// Default: 4 hours. If hours_worked &lt; HalfDayThresholdHours → Absent.
    /// If HalfDayThresholdHours ≤ hours_worked &lt; full shift hours → Half Day.
    /// </summary>
    public decimal HalfDayThresholdHours { get; set; } = 4m;

    /// <summary>
    /// If the employee checks out this many minutes before the shift end time,
    /// the record is flagged as Early Exit (status = "Early Exit").
    /// </summary>
    public int EarlyExitThresholdMinutes { get; set; } = 60;

    public bool IsNightShift { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
