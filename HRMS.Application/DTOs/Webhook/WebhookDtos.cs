namespace HRMS.Application.DTOs.Webhook;

public class CreateWebhookDto
{
    public string EventType { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>Tenant discriminator — tests and security checks pass CompanyId in the DTO.</summary>
    public int? CompanyId { get; set; }

    /// <summary>
    /// HMAC-SHA256 signing secret. Tests use SecretKey; the entity stores it as Secret.
    /// When provided, this overrides the auto-generated secret.
    /// </summary>
    public string? SecretKey { get; set; }
}

public class WebhookDto
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
