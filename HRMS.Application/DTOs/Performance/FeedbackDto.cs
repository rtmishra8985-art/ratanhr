namespace HRMS.Application.DTOs.Performance;

public record CreateFeedbackDto(
    string ToEmployeeId,
    string FeedbackText,
    string FeedbackType,
    bool IsAnonymous
);

public class FeedbackListDto
{
    public int Id { get; set; }
    public string FromEmployeeId { get; set; } = string.Empty;
    public string ToEmployeeId { get; set; } = string.Empty;
    public string FeedbackText { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
}
