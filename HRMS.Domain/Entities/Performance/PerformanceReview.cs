using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Performance;

public class PerformanceReview : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public string EmployeeId { get; set; } = string.Empty;
    public int ReviewerId { get; set; } // FK to users
    public int? PerformanceCycleId { get; set; }
    public string ReviewType { get; set; } = "Manager"; // Self, Manager, Peer, 360
    public string Status { get; set; } = "Pending"; // Pending, In Progress, Submitted, Acknowledged
    public decimal? SelfRating { get; set; }   // 1.0 – 5.0
    public decimal? ManagerRating { get; set; } // 1.0 – 5.0
    public decimal? FinalRating { get; set; }   // 1.0 – 5.0
    public string SelfComments { get; set; } = string.Empty;
    public string ManagerComments { get; set; } = string.Empty;
    public string HrComments { get; set; } = string.Empty;
    public string OverallComments { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
