using HRMS.Domain.Common;
namespace HRMS.Domain.Entities.Recruitment;

public class Interview : ICompanyOwned
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    int? ICompanyOwned.CompanyId => CompanyId;
    public int CandidateId { get; set; }
    public int? JobRequisitionId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string InterviewType { get; set; } = "Technical"; // Technical, HR, Managerial, Final
    public string Venue { get; set; } = string.Empty; // address or "Virtual – Google Meet"
    public string InterviewerNames { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled, No Show
    public int? FeedbackScore { get; set; } // 1-10
    public string FeedbackNotes { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty; // Proceed, Reject, Hold, Offer
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
