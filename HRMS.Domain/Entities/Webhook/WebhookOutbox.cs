using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Webhook;

/// <summary>
/// Outbox entry for reliable webhook delivery.
/// Added to satisfy test requirements for WebhookServiceTests.
/// </summary>
public class WebhookOutbox
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int SubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    /// <summary>HMAC-SHA256 signature of the payload — included in X-Webhook-Signature header.</summary>
    public string? Signature { get; set; }
    /// <summary>Pending | Sent | Failed</summary>
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
