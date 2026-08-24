using HRMS.Application.DTOs.Webhook;

namespace HRMS.Application.Interfaces;

public interface IWebhookService
{
    Task<List<WebhookDto>> ListAsync(int? companyId);
    Task<WebhookDto> RegisterAsync(int? companyId, CreateWebhookDto dto);
    Task<bool> DeleteAsync(int id, int? companyId);
    /// <summary>
    /// Fire-and-forget POST to all active subscriptions for this event/company.
    /// Retries 3× with exponential back-off via background queue.
    /// </summary>
    Task DispatchAsync(string eventType, int? companyId, object payload);

    // ── Test-friendly aliases ─────────────────────────────────────────────────
    /// <summary>Registers a webhook using CompanyId from the DTO. Returns the new subscription Id.</summary>
    Task<int> RegisterWebhookAsync(CreateWebhookDto dto);
    /// <summary>Lists active subscriptions for a company, respecting cancellation.</summary>
    Task<List<WebhookDto>> GetWebhooksAsync(int? companyId, CancellationToken ct = default);
    /// <summary>Deletes (deactivates) a webhook subscription, scoped to the owning company.</summary>
    Task<bool> DeleteWebhookAsync(int id, int? companyId);
    /// <summary>
    /// Enqueues an outbox entry for each active matching subscription.
    /// Writes a signed WebhookOutbox record; background dispatcher delivers it.
    /// </summary>
    Task DispatchEventAsync(int companyId, string eventType, string payload);
}
