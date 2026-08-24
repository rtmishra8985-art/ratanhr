namespace HRMS.Domain.Entities.Email;

public class EmailQueueItem
{
    public int Id { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending|Sent|Failed
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextRetryAt { get; set; }
}
