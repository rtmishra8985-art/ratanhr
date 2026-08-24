using HRMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Webhook;

public class WebhookSubscription : ICompanyOwned
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    /// <summary>e.g. employee.created | leave.approved | payroll.processed</summary>
    public string EventType { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    /// <summary>HMAC-SHA256 signing secret</summary>
    public string Secret { get; set; } = string.Empty;
    /// <summary>Alias for Secret — tests use SecretKey.</summary>
    [NotMapped] public string SecretKey { get => Secret; set => Secret = value; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
