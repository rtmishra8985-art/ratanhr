using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Email;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class EmailQueueService : IEmailQueueService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IEmailService _email;
    private readonly ILogger<EmailQueueService> _logger;

    public EmailQueueService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IEmailService email,
        ILogger<EmailQueueService> logger)
    {
        _dbFactory = dbFactory;
        _email     = email;
        _logger    = logger;
    }

    public async Task EnqueueAsync(string to, string subject, string htmlBody)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.EmailQueue.Add(new EmailQueueItem
        {
            ToAddress = to,
            Subject   = subject,
            HtmlBody  = htmlBody,
            Status    = "Pending",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<int> ProcessPendingAsync(int batchSize = 20)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var items = await db.EmailQueue
            .Where(e => e.Status == "Pending"
                     && (e.NextRetryAt == null || e.NextRetryAt <= now))
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

        int processed = 0;
        foreach (var item in items)
        {
            try
            {
                await _email.SendAsync(item.ToAddress, item.Subject, item.HtmlBody);
                item.Status = "Sent";
                item.SentAt = DateTime.UtcNow;
                item.LastError = null;
                processed++;
            }
            catch (Exception ex)
            {
                item.RetryCount++;
                item.LastError = ex.Message;
                if (item.RetryCount >= 3)
                {
                    item.Status = "Failed";
                    _logger.LogWarning("EmailQueue item {Id} permanently failed after 3 retries.", item.Id);
                }
                else
                {
                    item.Status      = "Pending";
                    // Exponential backoff: 2^retryCount minutes
                    item.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, item.RetryCount));
                    _logger.LogWarning("EmailQueue item {Id} failed, retry {Count} in {Min} min.",
                        item.Id, item.RetryCount, Math.Pow(2, item.RetryCount));
                }
            }
        }

        if (items.Any()) await db.SaveChangesAsync();
        return processed;
    }
}
