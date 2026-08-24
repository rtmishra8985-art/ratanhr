$smtpServer = "smtp-relay.brevo.com"
$smtpPort = 465
$username = "b5ef15001@smtp-brevo.com"
$password = "xkeysib-e8fc2e1e5e8c1c8c9d7f8e9c0d1e2f3g4h5i6j7k8l9m0n1o2p3q4r5s6t7"
$fromEmail = "rtmishra8985@gmail.com"
$toEmail = "rtmishra7040@gmail.com"
$subject = "RatanHR On-Premise TEST EMAIL"

Write-Host ""
Write-Host "========================================================================"
Write-Host "  RatanHR Phase 8 - ON-PREMISE TEST EMAIL (Brevo SMTP)"
Write-Host "========================================================================"
Write-Host ""

try {
    Write-Host "[1/3] Configuration Check"
    Write-Host "  SMTP Host: $smtpServer"
    Write-Host "  SMTP Port: $smtpPort"
    Write-Host "  Protocol: SSL/TLS"
    Write-Host "  From: $fromEmail"
    Write-Host "  To: $toEmail"
    Write-Host ""
    
    Write-Host "[2/3] Connecting and Sending Email..."
    Write-Host "  [Connecting to SMTP server...]"
    
    $smtp = New-Object System.Net.Mail.SmtpClient($smtpServer, $smtpPort)
    $smtp.EnableSsl = $true
    $smtp.Timeout = 15000
    
    Write-Host "  [Creating email message...]"
    $mail = New-Object System.Net.Mail.MailMessage($fromEmail, $toEmail)
    $mail.Subject = $subject
    $mail.IsBodyHtml = $true
    
    $htmlBody = @"
<html>
  <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <h1>RatanHR On-Premise Email Test</h1>
    <h2>On-Premise SMTP Configuration Test</h2>
    <p><strong>Status:</strong> ON-PREMISE BREVO SMTP CONFIGURED</p>
    <h3>Test Details</h3>
    <ul>
      <li><strong>From:</strong> $fromEmail</li>
      <li><strong>To:</strong> $toEmail</li>
      <li><strong>SMTP Server:</strong> $smtpServer</li>
      <li><strong>Port:</strong> $smtpPort</li>
      <li><strong>Protocol:</strong> SSL/TLS</li>
      <li><strong>Time:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</li>
    </ul>
    <h3>Verification</h3>
    <p>✓ Brevo SMTP connection established</p>
    <p>✓ SSL/TLS encryption working</p>
    <p>✓ On-premise email service ready</p>
    <hr />
    <p><em>This is a real email sent from on-premise environment</em></p>
  </body>
</html>
"@
    
    $mail.Body = $htmlBody
    
    Write-Host "  [Authenticating with Brevo...]"
    $cred = New-Object System.Net.NetworkCredential($username, $password)
    $smtp.Credentials = $cred
    
    Write-Host "  [Sending email via SSL/TLS...]"
    $smtp.Send($mail)
    
    Write-Host ""
    Write-Host "========================================================================"
    Write-Host "SUCCESS - ON-PREMISE EMAIL SENT TO BREVO"
    Write-Host "========================================================================"
    Write-Host ""
    Write-Host "[3/3] Summary"
    Write-Host "  From: $fromEmail"
    Write-Host "  To: $toEmail"
    Write-Host "  Subject: $subject"
    Write-Host ("  SMTP: " + $smtpServer + ":" + $smtpPort)
    Write-Host "  Protocol: SSL/TLS (Port 465)"
    Write-Host "  Status: SENT SUCCESSFULLY"
    Write-Host ""
    Write-Host "Next Steps:"
    Write-Host ("  1. Check email inbox: " + $toEmail)
    Write-Host "  2. Wait 1-2 minutes for delivery"
    Write-Host "  3. Verify email from: $fromEmail"
    Write-Host "  4. On-Premise SMTP is working!"
    Write-Host ""
    
    $mail.Dispose()
    $smtp.Dispose()
}
catch {
    Write-Host ""
    Write-Host "========================================================================"
    Write-Host "ERROR - EMAIL FAILED"
    Write-Host "========================================================================"
    Write-Host ""
    Write-Host ("Error: " + $_.Exception.Message)
    Write-Host ""
    Write-Host "Troubleshooting:"
    Write-Host "  1. Check internet connection"
    Write-Host "  2. Verify port 465 not blocked by firewall"
    Write-Host "  3. Confirm Brevo credentials are correct"
    Write-Host "  4. Check firewall/network rules"
    Write-Host ""
}
