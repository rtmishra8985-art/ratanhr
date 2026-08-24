using HRMS.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Production email delivery via MailKit.
/// Configure via appsettings:
///   Email:Host, Email:Port, Email:UseSsl, Email:Username, Email:Password,
///   Email:FromAddress, Email:FromName, Email:AppBaseUrl
/// When Email:Host is empty the service falls back to logging the message
/// (dev/test convenience) so the rest of the app still works without an SMTP server.
///
/// IMPORTANT: In Production, an unconfigured or failing SMTP setup is logged at
/// Warning/Error level (not Information) so it is not swallowed by the
/// "Serilog:MinimumLevel:Default=Warning" override in appsettings.Production.json.
/// EmailHealthCheck also surfaces this on /health so it's visible to monitoring,
/// not just discoverable by grepping logs after a client complains.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly IHostEnvironment _env;

    public EmailService(IConfiguration config, ILogger<EmailService> logger, IHostEnvironment env)
    {
        _config = config;
        _logger = logger;
        _env = env;
    }

    private string AppBaseUrl    => _config["Email:AppBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
    private string CompanyName    => _config["App:CompanyName"] ?? "HRMS System";

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetLink)
    {
        var subject = "HRMS – Password Reset Request";
        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#1a3a5c;padding:24px 32px;border-radius:8px 8px 0 0;'>
    <h2 style='color:#fff;margin:0;'>HRMS Password Reset</h2>
  </div>
  <div style='background:#f8f9fa;padding:32px;border-radius:0 0 8px 8px;border:1px solid #dee2e6;'>
    <p style='font-size:15px;'>Hello <strong>{toName}</strong>,</p>
    <p>We received a request to reset your HRMS account password.
       Click the button below to set a new password. This link expires in <strong>30 minutes</strong>.</p>
    <div style='text-align:center;margin:32px 0;'>
      <a href='{resetLink}' style='background:#f7b731;color:#1a1a1a;padding:14px 32px;
         border-radius:6px;text-decoration:none;font-weight:bold;font-size:15px;display:inline-block;'>
        Reset My Password
      </a>
    </div>
    <p style='font-size:13px;color:#6c757d;'>
      If you did not request this, you can safely ignore this email.
      Your password will not change until you click the link above.<br><br>
      If the button doesn't work, copy and paste this URL into your browser:<br>
      <a href='{resetLink}' style='color:#2980b9;word-break:break-all;'>{resetLink}</a>
    </p>
  </div>
  <p style='font-size:12px;color:#adb5bd;text-align:center;margin-top:16px;'>
    © HRMS – {CompanyName} This is an automated message; please do not reply.
  </p>
</div>";

        await SendAsync(toEmail, toName, subject, html);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName, string employeeId, string tempPassword)
    {
        // FIX: previously linked to "{AppBaseUrl}/login.html", a static page removed
        // from wwwroot (legacy *.html pages were archived under /legacy-ui — see
        // Program.cs). The welcome email's "Go to Employee Portal" button pointed at a
        // 404 for every newly onboarded employee. "/login" is the real React SPA route.
        var loginUrl = $"{AppBaseUrl}/login";
        var subject = "Welcome to HRMS – Your Employee Account is Ready";
        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#1a3a5c;padding:24px 32px;border-radius:8px 8px 0 0;'>
    <h2 style='color:#fff;margin:0;'>Welcome to HRMS!</h2>
  </div>
  <div style='background:#f8f9fa;padding:32px;border-radius:0 0 8px 8px;border:1px solid #dee2e6;'>
    <p style='font-size:15px;'>Hello <strong>{toName}</strong>,</p>
    <p>Your HRMS employee account has been created. Here are your login credentials:</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr>
        <td style='padding:10px 16px;background:#e9ecef;font-weight:bold;border-radius:4px 0 0 0;'>Employee ID</td>
        <td style='padding:10px 16px;background:#fff;border:1px solid #dee2e6;font-family:monospace;'>{employeeId}</td>
      </tr>
      <tr>
        <td style='padding:10px 16px;background:#e9ecef;font-weight:bold;border-radius:0 0 0 4px;'>Temporary Password</td>
        <td style='padding:10px 16px;background:#fff;border:1px solid #dee2e6;font-family:monospace;'>{tempPassword}</td>
      </tr>
    </table>
    <p style='color:#dc3545;font-size:13px;'>
      ⚠️ You will be required to change this password on your first login. Please keep it confidential.
    </p>
    <div style='text-align:center;margin:24px 0;'>
      <a href='{loginUrl}' style='background:#f7b731;color:#1a1a1a;padding:14px 32px;
         border-radius:6px;text-decoration:none;font-weight:bold;font-size:15px;display:inline-block;'>
        Go to Employee Portal
      </a>
    </div>
  </div>
  <p style='font-size:12px;color:#adb5bd;text-align:center;margin-top:16px;'>
    © HRMS – {CompanyName}
  </p>
</div>";

        await SendAsync(toEmail, toName, subject, html);
    }

    public async Task SendLeaveDecisionAsync(string toEmail, string toName, string leaveType,
                                             string fromDate, string toDate, bool approved, string? remarks)
    {
        var status = approved ? "Approved ✅" : "Rejected ❌";
        var color  = approved ? "#198754" : "#dc3545";
        var subject = $"HRMS – Leave Request {(approved ? "Approved" : "Rejected")}";
        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
  <div style='background:#1a3a5c;padding:24px 32px;border-radius:8px 8px 0 0;'>
    <h2 style='color:#fff;margin:0;'>Leave Request Update</h2>
  </div>
  <div style='background:#f8f9fa;padding:32px;border-radius:0 0 8px 8px;border:1px solid #dee2e6;'>
    <p style='font-size:15px;'>Hello <strong>{toName}</strong>,</p>
    <p>Your leave request has been updated:</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr><td style='padding:8px 16px;background:#e9ecef;font-weight:bold;'>Leave Type</td>
          <td style='padding:8px 16px;background:#fff;border:1px solid #dee2e6;'>{leaveType}</td></tr>
      <tr><td style='padding:8px 16px;background:#e9ecef;font-weight:bold;'>From</td>
          <td style='padding:8px 16px;background:#fff;border:1px solid #dee2e6;'>{fromDate}</td></tr>
      <tr><td style='padding:8px 16px;background:#e9ecef;font-weight:bold;'>To</td>
          <td style='padding:8px 16px;background:#fff;border:1px solid #dee2e6;'>{toDate}</td></tr>
      <tr><td style='padding:8px 16px;background:#e9ecef;font-weight:bold;'>Status</td>
          <td style='padding:8px 16px;background:#fff;border:1px solid #dee2e6;color:{color};font-weight:bold;'>{status}</td></tr>
      {(string.IsNullOrWhiteSpace(remarks) ? "" :
        $"<tr><td style='padding:8px 16px;background:#e9ecef;font-weight:bold;'>Remarks</td><td style='padding:8px 16px;background:#fff;border:1px solid #dee2e6;'>{remarks}</td></tr>")}
    </table>
    <p style='font-size:13px;color:#6c757d;'>Log in to the Employee Portal to view your leave balance.</p>
  </div>
  <p style='font-size:12px;color:#adb5bd;text-align:center;margin-top:16px;'>
    © HRMS – {CompanyName}
  </p>
</div>";

        await SendAsync(toEmail, toName, subject, html);
    }

    // ── Internal send helper ───────────────────────────────────────────────

    /// <summary>
    /// Generic send used by EmailQueueService. Uses a blank display-name since
    /// the queue only stores the raw address.
    /// </summary>
    public Task SendAsync(string toEmail, string subject, string htmlBody)
        => SendAsync(toEmail, toEmail, subject, htmlBody);

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host = _config["Email:Host"];

        // Dev/test fallback: log instead of sending when SMTP is not configured.
        if (string.IsNullOrWhiteSpace(host))
        {
            if (_env.IsProduction())
            {
                // Warning, not Information — Production's default Serilog level is
                // Warning, so Information-level logs here would vanish silently.
                _logger.LogWarning(
                    "SMTP is not configured (Email:Host is empty). Password-reset, welcome, and " +
                    "leave-decision emails will NOT be delivered — only logged. " +
                    "Set EMAIL_HOST and related Email:* settings before go-live.");

                // Only mark /health degraded in Production — a blank Email:Host in
                // dev/test is expected behavior, not an incident.
                EmailHealthCheck.LastFailureUtc = DateTime.UtcNow;
                EmailHealthCheck.LastFailureReason = "Email:Host is not configured";
            }
            else
            {
                _logger.LogInformation(
                    "[EMAIL – no SMTP configured] Message delivery skipped.");
            }
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _config["Email:FromName"] ?? "HRMS System",
            _config["Email:FromAddress"] ?? _config["Email:Username"] ?? "noreply@hrms.com"));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var port    = _config.GetValue<int>("Email:Port", 587);
            var useSsl  = _config.GetValue<bool>("Email:UseSsl", false);
            var secOpt  = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

            // FIX 4: Hard 15-second timeout for both connect and send.
            // A misconfigured or slow SMTP relay can block the calling thread indefinitely
            // without a CancellationToken — 15 s is sufficient for normal delivery and
            // prevents the EmailQueueWorker thread from stalling the entire queue.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await client.ConnectAsync(host, port, secOpt, cts.Token);
            var username = _config["Email:Username"];
            var password = _config["Email:Password"];
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                await client.AuthenticateAsync(username, password, cts.Token);
            await client.SendAsync(message, cts.Token);
            await client.DisconnectAsync(true, cts.Token);
            _logger.LogInformation("Email delivery completed.");
            EmailHealthCheck.LastFailureUtc = null;
            EmailHealthCheck.LastFailureReason = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email delivery failed.");
            EmailHealthCheck.LastFailureUtc = DateTime.UtcNow;
            EmailHealthCheck.LastFailureReason = ex.Message;
            // Don't rethrow — email failure must not break the calling operation
            // (e.g. password reset should still succeed; the token is already persisted).
        }
    }
}
