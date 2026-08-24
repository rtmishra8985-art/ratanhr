using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Timesheet;

/// <summary>
/// Weekly-aggregate timesheet header. Groups per-day <see cref="TimesheetEntry"/> rows
/// into a single approvable unit covering one calendar week.
/// </summary>
public class Timesheet : ICompanyOwned
{
    public int Id { get; set; }

    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int TimesheetId { get => Id; set => Id = value; }

    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant discriminator. EF Core global query filter scopes all reads
    /// to the caller's company.
    /// </summary>
    public int? CompanyId { get; set; }

    /// <summary>Monday of the week this timesheet covers (inclusive).</summary>
    public DateOnly WeekStartDate { get; set; }

    /// <summary>Sunday of the week this timesheet covers (inclusive).</summary>
    public DateOnly WeekEndDate { get; set; }

    /// <summary>Sum of HoursWorked across all child TimesheetEntry rows for the week.</summary>
    public decimal TotalHours { get; set; }

    /// <summary>
    /// Lifecycle status of the weekly timesheet.
    /// Values: Draft | Submitted | Approved | Rejected
    /// </summary>
    public string Status { get; set; } = "Draft";

    public string? ManagerRemarks { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
