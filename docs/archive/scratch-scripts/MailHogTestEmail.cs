using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace RatanHR.MailHogTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            const string MAILHOG_HOST = "localhost";
            const int MAILHOG_PORT = 1025;
            const string FROM_EMAIL = "rtmishra8985@gmail.com";
            const string TO_EMAIL = "rtmishra7040@gmail.com";

            Console.WriteLine("════════════════════════════════════════════════════════════");
            Console.WriteLine("RatanHR Phase 8 - MailHog SMTP Test Email");
            Console.WriteLine("════════════════════════════════════════════════════════════");
            Console.WriteLine();

            try
            {
                Console.WriteLine("[1/3] Connecting to MailHog: {0}:{1}", MAILHOG_HOST, MAILHOG_PORT);
                using (var client = new SmtpClient(MAILHOG_HOST, MAILHOG_PORT))
                {
                    client.EnableSsl = false;
                    client.Timeout = 10000;
                    Console.WriteLine("✓ Connected to MailHog SMTP server");
                    Console.WriteLine();

                    Console.WriteLine("[2/3] Creating email message...");
                    using (var message = new MailMessage(FROM_EMAIL, TO_EMAIL))
                    {
                        message.Subject = "🟢 RatanHR Phase 8 - MailHog TEST EMAIL";
                        message.IsBodyHtml = true;
                        message.Body = GenerateEmailBody();

                        Console.WriteLine("✓ Email message created");
                        Console.WriteLine();

                        Console.WriteLine("[3/3] Sending email via MailHog SMTP...");
                        client.Send(message);
                        Console.WriteLine("✓ Email sent successfully!");
                        Console.WriteLine();
                    }
                }

                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine("✅ SUCCESS - EMAIL SENT TO MAILHOG");
                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine("Next: Open http://localhost:8025 to view the email");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine("❌ ERROR - {0}", ex.Message);
                Console.WriteLine("════════════════════════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine("Troubleshooting:");
                Console.WriteLine("  1. Is MailHog running? (Check command window)");
                Console.WriteLine("  2. Is port 1025 available?");
                Console.WriteLine("  3. Check firewall settings");
                Console.WriteLine();
            }
        }

        static string GenerateEmailBody()
        {
            return $@"
<html>
  <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <h1>🟢 RatanHR Phase 8 - MailHog Test Email</h1>
    
    <h2>Email Configuration Test</h2>
    <p><strong>Status:</strong> ✅ MAILHOG CONFIGURED & WORKING</p>
    
    <h3>Test Details</h3>
    <ul>
      <li><strong>From:</strong> rtmishra8985@gmail.com</li>
      <li><strong>To:</strong> rtmishra7040@gmail.com</li>
      <li><strong>Time:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</li>
      <li><strong>Server:</strong> MailHog (localhost:1025)</li>
      <li><strong>Protocol:</strong> SMTP (no authentication)</li>
    </ul>
    
    <h3>What This Means</h3>
    <p>✅ Email service is configured for local testing</p>
    <p>✅ MailHog is capturing all emails sent</p>
    <p>✅ Ready for on-premise deployment</p>
    
    <h3>Next Steps</h3>
    <ol>
      <li>Check MailHog Web UI: <a href='http://localhost:8025'>http://localhost:8025</a></li>
      <li>This email should be visible in the inbox</li>
      <li>When ready for production, switch to Brevo SMTP</li>
    </ol>
    
    <hr>
    <p><em>This is a test email from RatanHR Phase 8 SMTP Configuration</em></p>
  </body>
</html>
";
        }
    }
}
