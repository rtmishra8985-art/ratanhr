namespace HRMS.Application.Interfaces;

public interface IEmailQueueService
{
    Task EnqueueAsync(string to, string subject, string htmlBody);
    Task<int> ProcessPendingAsync(int batchSize = 20);
}
