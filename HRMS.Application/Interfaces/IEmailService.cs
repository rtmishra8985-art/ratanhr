namespace HRMS.Application.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string toName, string resetLink);
    Task SendWelcomeAsync(string toEmail, string toName, string employeeId, string tempPassword);
    Task SendLeaveDecisionAsync(string toEmail, string toName, string leaveType,
                                string fromDate, string toDate, bool approved, string? remarks);

    /// <summary>
    /// Generic send — used by EmailQueueService to dispatch queued email items.
    /// </summary>
    Task SendAsync(string toEmail, string subject, string htmlBody);
}
