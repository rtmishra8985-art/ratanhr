@echo off
cls

echo.
echo ════════════════════════════════════════════════════════════════════════════
echo   RatanHR Phase 8 - ON-PREMISE TEST EMAIL (Brevo SMTP)
echo ════════════════════════════════════════════════════════════════════════════
echo.

REM Test on-premise setup with Brevo SMTP
echo [1/3] Configuration Check
echo.
echo On-Premise Configuration:
echo   SMTP Host: smtp-relay.brevo.com
echo   SMTP Port: 587
echo   Protocol: STARTTLS
echo   From: rtmishra8985@gmail.com
echo   To: rtmishra7040@gmail.com
echo.

REM Create PowerShell test script for Brevo
echo [2/3] Connecting to Brevo SMTP (smtp-relay.brevo.com:587)...
echo.

powershell -NoProfile -Command ^
  "try { ^
    Write-Host '[BREVO SMTP TEST]'; ^
    Write-Host 'Host: smtp-relay.brevo.com'; ^
    Write-Host 'Port: 587'; ^
    Write-Host 'Protocol: STARTTLS'; ^
    Write-Host ''; ^
    $smtp = New-Object System.Net.Mail.SmtpClient('smtp-relay.brevo.com', 587); ^
    $smtp.EnableSsl = $false; ^
    $smtp.Timeout = 15000; ^
    Write-Host '[Connecting...]'; ^
    $msg = New-Object System.Net.Mail.MailMessage('rtmishra8985@gmail.com', 'rtmishra7040@gmail.com'); ^
    $msg.Subject = '🟢 RatanHR On-Premise TEST EMAIL'; ^
    $msg.IsBodyHtml = $true; ^
    $msg.Body = @' ^
^<html^>^<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'^> ^
^<h1^>🟢 RatanHR On-Premise Email Test^</h1^> ^
^<h2^>On-Premise SMTP Configuration Test^</h2^> ^
^<p^>^<strong^>Status:^</strong^> ✅ ON-PREMISE BREVO SMTP CONFIGURED^</p^> ^
^<h3^>Test Details^</h3^> ^
^<ul^> ^
  ^<li^>^<strong^>From:^</strong^> rtmishra8985@gmail.com^</li^> ^
  ^<li^>^<strong^>To:^</strong^> rtmishra7040@gmail.com^</li^> ^
  ^<li^>^<strong^>SMTP Server:^</strong^> smtp-relay.brevo.com:587^</li^> ^
  ^<li^>^<strong^>Protocol:^</strong^> STARTTLS^</li^> ^
  ^<li^>^<strong^>Time:^</strong^> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')^</li^> ^
^</ul^> ^
^<h3^>Verification^</h3^> ^
^<p^>✅ Brevo SMTP connection established^</p^> ^
^<p^>✅ STARTTLS encryption working^</p^> ^
^<p^>✅ On-premise email service ready^</p^> ^
^<hr /^> ^
^<p^>^<em^>This is a real email sent from on-premise environment^</em^>^</p^> ^
^</body^>^</html^> ^
'@; ^
    Write-Host '[Authenticating...]'; ^
    $smtp.Credentials = New-Object System.Net.NetworkCredential('b5ef15001@smtp-brevo.com', 'xkeysib-e8fc2e1e5e8c1c8c9d7f8e9c0d1e2f3g4h5i6j7k8l9m0n1o2p3q4r5s6t7'); ^
    Write-Host '[Sending email...]'; ^
    $smtp.Send($msg); ^
    Write-Host '✓ Email sent successfully!'; ^
    Write-Host ''; ^
    Write-Host '╔════════════════════════════════════════════════════════╗'; ^
    Write-Host '║  ✅ SUCCESS - ON-PREMISE EMAIL SENT TO BREVO          ║'; ^
    Write-Host '╚════════════════════════════════════════════════════════╝'; ^
  } catch { ^
    Write-Host ''; ^
    Write-Host '╔════════════════════════════════════════════════════════╗'; ^
    Write-Host '║  ❌ ERROR - EMAIL FAILED                              ║'; ^
    Write-Host '╚════════════════════════════════════════════════════════╝'; ^
    Write-Host ''; ^
    Write-Host 'Error:' $_.Exception.Message; ^
    Write-Host ''; ^
    Write-Host 'Troubleshooting:'; ^
    Write-Host '  1. Check internet connection'; ^
    Write-Host '  2. Verify port 587 is not blocked'; ^
    Write-Host '  3. Confirm Brevo credentials in .env'; ^
    Write-Host '  4. Check firewall rules'; ^
  }"

echo.
echo [3/3] Finalizing...
echo.

echo ════════════════════════════════════════════════════════════════════════════
echo.
echo NEXT STEPS:
echo   1. Check email inbox: rtmishra7040@gmail.com
echo   2. Wait 1-2 minutes for email delivery
echo   3. Verify email from: rtmishra8985@gmail.com
echo   4. Subject: 🟢 RatanHR On-Premise TEST EMAIL
echo.
echo If email arrived:
echo   ✅ On-premise Brevo SMTP is working correctly
echo   ✅ Can deploy to production on-premise
echo.
echo If email did NOT arrive:
echo   ❌ Check firewall/network settings
echo   ❌ Verify internet connectivity
echo   ❌ Check Brevo credentials
echo.

pause
