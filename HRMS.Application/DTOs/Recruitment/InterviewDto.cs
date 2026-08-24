namespace HRMS.Application.DTOs.Recruitment;

public record ScheduleInterviewDto(
    int CandidateId,
    int? JobRequisitionId,
    DateTime ScheduledAt,
    string InterviewType,
    string Venue,
    string InterviewerNames
);

public record UpdateInterviewDto(
    DateTime ScheduledAt,
    string InterviewType,
    string Venue,
    string InterviewerNames,
    string Status
);

public record SubmitFeedbackDto(
    int FeedbackScore,
    string FeedbackNotes,
    string Recommendation,
    string Status
);

public class InterviewListDto
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public int? JobRequisitionId { get; set; }
    public string? JobTitle { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string InterviewType { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public string InterviewerNames { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? FeedbackScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
