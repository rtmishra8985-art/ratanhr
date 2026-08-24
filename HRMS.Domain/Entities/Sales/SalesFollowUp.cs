using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Sales;

public class SalesFollowUp : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    public int SalesLeadId { get; set; }
    public string Notes { get; set; } = string.Empty;

    public DateTime ReminderDate { get; set; }
    public TimeSpan? ReminderTime { get; set; }

    /// <summary>Phone / WhatsApp / Email / Meeting</summary>
    public string Mode { get; set; } = "Phone";

    /// <summary>Pending / Completed</summary>
    public string Status { get; set; } = "Pending";

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
