namespace HRMS.Application.DTOs.Attendance;

public class ShiftDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int GracePeriodMinutes { get; set; }
    public bool IsNightShift { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateShiftDto
{
    public int CompanyId { get; set; }
    public string ShiftName { get; set; } = string.Empty;

    /// <summary>Alias for ShiftName — tests use Name = "Day Shift".</summary>
    public string Name { get => ShiftName; set => ShiftName = value; }

    public string StartTime { get; set; } = string.Empty;  // "HH:mm"
    public string EndTime { get; set; } = string.Empty;    // "HH:mm"
    public int GracePeriodMinutes { get; set; } = 15;
    public bool IsNightShift { get; set; }
}
