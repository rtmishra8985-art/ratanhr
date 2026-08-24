using HRMS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class EmailQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueWorker> _logger;

    public EmailQueueWorker(IServiceScopeFactory scopeFactory, ILogger<EmailQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("EmailQueueWorker started.");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
                var count = await svc.ProcessPendingAsync(20);
                if (count > 0)
                    _logger.LogInformation("EmailQueueWorker: dispatched {Count} emails.", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailQueueWorker encountered an error.");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
        _logger.LogInformation("EmailQueueWorker stopped.");
    }
}
