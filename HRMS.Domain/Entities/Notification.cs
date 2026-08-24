namespace HRMS.Domain.Entities;

using HRMS.Domain.Common;

/// <summary>In-app notification for a user.</summary>
public class Notification : ICompanyOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>Tenant copied from the recipient user at creation time.</summary>
    public int? CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info"; // info | success | warning | error
    public string? EntityType { get; set; }    // e.g. "LeaveRequest", "Payslip"
    public string? EntityId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    int? ICompanyOwned.CompanyId => CompanyId;
}
