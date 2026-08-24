using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.Leave;

public class LeaveBalanceAdjustmentDto
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public int Year { get; set; }
    public int Days { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int AdjustedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateLeaveBalanceAdjustmentDto
{
    [Required]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    public int LeaveTypeId { get; set; }

    [Required]
    public int Year { get; set; }

    [Required, Range(-365, 365)]
    public int Days { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class LeaveCarryForwardDto
{
    [Required, Range(2000, 2100)]
    public int FromYear { get; set; }

    [Required, Range(2000, 2100)]
    public int ToYear { get; set; }

    /// <summary>Max carry-forward days per leave type (0 = unlimited).</summary>
    public int MaxDays { get; set; } = 0;

    /// <summary>Only carry forward for this company. Null = all companies.</summary>
    public int? CompanyId { get; set; }
}
