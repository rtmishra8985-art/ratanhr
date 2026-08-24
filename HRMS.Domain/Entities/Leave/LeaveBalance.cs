using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Leave;

/// <summary>
/// Tracks the leave balance per employee per leave type per year.
/// Added to satisfy test requirements for LeaveServiceTests and LeaveIntegrationTests.
/// </summary>
public class LeaveBalance : ICompanyOwned
{
    public int BalanceId { get; set; }
    /// <summary>PK alias.</summary>
    [NotMapped] public int Id { get => BalanceId; set => BalanceId = value; }
    public int? CompanyId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public int TotalDays { get; set; }
    public int AvailableDays { get; set; }
    public int UsedDays { get; set; }
    public int PendingDays { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
