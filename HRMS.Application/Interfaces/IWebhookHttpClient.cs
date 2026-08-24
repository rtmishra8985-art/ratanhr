namespace HRMS.Application.Interfaces;

/// <summary>Abstraction over HTTP webhook delivery — allows mocking in unit tests.</summary>
public interface IWebhookHttpClient
{
    Task<bool> PostAsync(string targetUrl, string serializedBody, CancellationToken ct = default);
}
