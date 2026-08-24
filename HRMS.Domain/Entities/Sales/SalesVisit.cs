using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Sales;

public class SalesVisit : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int? BranchId { get; set; }

    public int? SalesLeadId { get; set; }
    public int? SalesCustomerId { get; set; }

    public string VisitedEmployeeId { get; set; } = string.Empty;

    // Check-in
    public decimal? CheckInLatitude { get; set; }
    public decimal? CheckInLongitude { get; set; }
    public string CheckInAddress { get; set; } = string.Empty;
    public string? CheckInPhotoPath { get; set; }
    public DateTime? CheckInTime { get; set; }

    // Check-out
    public DateTime? CheckOutTime { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal? DistanceKm { get; set; }

    public string Notes { get; set; } = string.Empty;

    /// <summary>CheckedIn / CheckedOut</summary>
    public string Status { get; set; } = "CheckedIn";

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
