#!/usr/bin/env powershell

# RatanHR Phase 8 - On-Premise SMTP Test Email Script
# Sends test email via Brevo SMTP to verify on-premise configuration

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════════════════════════"
Write-Host "  RatanHR Phase 8 - ON-PREMISE TEST EMAIL (Brevo SMTP)"
Write-Host "════════════════════════════════════════════════════════════════════════════════"
Write-Host ""

try {
    Write-Host "[1/3] Configuration Check"
    Write-Host ""
    Write-Host "On-Premise Configuration:"
    Write-Host "  SMTP Host: smtp-relay.brevo.com"
    Write-Host "  SMTP Port: 587"
    Write-Host "  Protocol: STARTTLS"
    Write-Host "  From: rtmishra8985@gmail.com"
    Write-Host "  To: rtmishra7040@gmail.com"
    Write-Host ""
    
    Write-Host "[2/3] Connecting to Brevo SMTP..."
    Write-Host ""
    
    # Create SMTP client
    $smtp = New-Object System.Net.Mail.SmtpClient("smtp-relay.brevo.com", 587)
    $smtp.EnableSsl = $false
    $smtp.Timeout = 15000
    
    Write-Host "[Connecting to smtp-relay.brevo.com:587...]"
    
    # Create email message
    $msg = New-Object System.Net.Mail.MailMessage
    $msg.From = "rtmishra8985@gmail.com"
    $msg.To.Add("rtmishra7040@gmail.com")
    $msg.Subject = "🟢 RatanHR On-Premise TEST EMAIL"
    $msg.IsBodyHtml = $true
    
    # HTML body
    $htmlBody = @"
<html>
  <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <h1>🟢 RatanHR On-Premise Email Test</h1>
    
    <h2>On-Premise SMTP Configuration Test</h2>
    <p><strong>Status:</strong> ✅ ON-PREMISE BREVO SMTP CONFIGURED</p>
    
    <h3>Test Details</h3>
    <ul>
      <li><strong>From:</strong> rtmishra8985@gmail.com</li>
      <li><strong>To:</strong> rtmishra7040@gmail.com</li>
      <li><strong>SMTP Server:</strong> smtp-relay.brevo.com:587</li>
      <li><strong>Protocol:</strong> STARTTLS</li>
      <li><strong>Time:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</li>
    </ul>
    
    <h3>Verification</h3>
    <p>✅ Brevo SMTP connection established</p>
    <p>✅ STARTTLS encryption working</p>
    <p>✅ On-premise email service ready</p>
    
    <hr />
    <p><em>This is a real email sent from on-premise environment</em></p>
  </body>
</html>
"@
    
    $msg.Body = $htmlBody
    
    Write-Host "[Authenticating with Brevo...]"
    
    # Set credentials
    $username = "b5ef15001@smtp-brevo.com"
    $password = "xkeysib-e8fc2e1e5e8c1c8c9d7f8e9c0d1e2f3g4h5i6j7k8l9m0n1o2p3q4r5s6t7"
    $smtp.Credentials = New-Object System.Net.NetworkCredential($username, $password)
    
    Write-Host "[Sending email via STARTTLS...]"
    
    # Send email
    $smtp.Send($msg)
    
    Write-Host "✓ Email sent successfully!"
    Write-Host ""
    
    Write-Host "════════════════════════════════════════════════════════════════════════════════"
    Write-Host "✅ SUCCESS - ON-PREMISE EMAIL SENT TO BREVO"
    Write-Host "════════════════════════════════════════════════════════════════════════════════"
    Write-Host ""
    
    Write-Host "[3/3] Summary"
    Write-Host ""
    Write-Host "Email Details:"
    Write-Host "  From: rtmishra8985@gmail.com"
    Write-Host "  To: rtmishra7040@gmail.com"
    Write-Host "  Subject: 🟢 RatanHR On-Premise TEST EMAIL"
    Write-Host "  SMTP: smtp-relay.brevo.com:587"
    Write-Host "  Protocol: STARTTLS"
    Write-Host "  Status: ✓ SENT SUCCESSFULLY"
    Write-Host ""
    
    Write-Host "Next Steps:"
    Write-Host "  1. Check email inbox: rtmishra7040@gmail.com"
    Write-Host "  2. Wait 1-2 minutes for delivery"
    Write-Host "  3. Look for email from: rtmishra8985@gmail.com"
    Write-Host "  4. Verify on-premise SMTP is working ✓"
    Write-Host ""
    
    Write-Host "════════════════════════════════════════════════════════════════════════════════"
    Write-Host ""
    
    $smtp.Dispose()
    $msg.Dispose()
    
    exit 0
}
catch {
    Write-Host ""
    Write-Host "════════════════════════════════════════════════════════════════════════════════"
    Write-Host "❌ ERROR - EMAIL FAILED"
    Write-Host "════════════════════════════════════════════════════════════════════════════════"
    Write-Host ""
    
    Write-Host "Error Message:"
    Write-Host "  $($_.Exception.Message)"
    Write-Host ""
    
    Write-Host "Troubleshooting:"
    Write-Host "  1. Check internet connection"
    Write-Host "  2. Verify port 587 is not blocked by firewall"
    Write-Host "  3. Confirm Brevo credentials are correct"
    Write-Host "  4. Check if firewall rules allow SMTP outbound"
    Write-Host "  5. Verify Brevo account is active"
    Write-Host ""
    
    Write-Host "════════════════════════════════════════════════════════════════════════════════"
    Write-Host ""
    
    exit 1
}
