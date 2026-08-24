using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Sales;

public class SalesTask : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Employee assigned to complete this task.</summary>
    public string? AssignedToEmployeeId { get; set; }

    /// <summary>Low / Medium / High / Critical</summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>Pending / In Progress / Completed / Cancelled</summary>
    public string Status { get; set; } = "Pending";

    public DateTime? Deadline { get; set; }
    public DateTime? ReminderDate { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
