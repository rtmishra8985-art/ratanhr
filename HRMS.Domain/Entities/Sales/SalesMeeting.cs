using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Sales;

public class SalesMeeting : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }

    public string Title { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public TimeSpan MeetingTime { get; set; }

    public string Location { get; set; } = string.Empty;
    public string? GoogleMapUrl { get; set; }

    /// <summary>Online / Offline</summary>
    public string MeetingType { get; set; } = "Offline";

    public string Outcome { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    /// <summary>Scheduled / Completed / Cancelled</summary>
    public string Status { get; set; } = "Scheduled";

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
