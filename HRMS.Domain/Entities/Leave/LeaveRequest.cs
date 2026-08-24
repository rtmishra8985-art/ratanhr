using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Leave;

/// <summary>An employee's leave application and its approval lifecycle.</summary>
public class LeaveRequest : ICompanyOwned
{
    public int Id { get; set; }
    /// <summary>Domain-prefixed PK alias — maps to Id.</summary>
    [NotMapped] public int LeaveRequestId { get => Id; set => Id = value; }
    public string EmployeeId { get; set; } = string.Empty; // Employee.EmployeeCode (business key)
    public int? CompanyId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalDays { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected | Cancelled
    public int? ApprovedByUserId { get; set; }
    public string? ApproverRemarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// True when this leave request was created by the demo-mode seed service
    /// (<see cref="HRMS.Infrastructure.Services.Demo.DemoSeedService"/>). Used by
    /// CleanupAsync to delete only demo leave requests and never touch real employee leave.
    /// </summary>
    public bool IsDemo { get; set; } = false;
}
