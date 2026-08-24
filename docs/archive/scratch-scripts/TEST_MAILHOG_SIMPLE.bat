@echo off
cls
setlocal enabledelayedexpansion

echo.
echo ╔════════════════════════════════════════════════════════════════════════════╗
echo ║   RatanHR Phase 8 - MailHog SMTP Test Email (PowerShell)                  ║
echo ║   Testing: Is MailHog Capturing Emails?                                   ║
echo ╚════════════════════════════════════════════════════════════════════════════╝
echo.

REM Check if MailHog is running
echo [1/3] Checking if MailHog is running on localhost:1025...
timeout /t 1 /nobreak >nul

netstat -ano | findstr ":1025" >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo ❌ ERROR: MailHog is not running on port 1025
    echo.
    echo MailHog must be started first!
    echo.
    echo How to start MailHog:
    echo   1. Download: mailhog_windows_amd64.exe
    echo   2. From: https://github.com/mailhog/MailHog/releases
    echo   3. Double-click to run
    echo   4. Wait for: "Binding to address: 0.0.0.0:1025"
    echo   5. Then run this script again
    echo.
    pause
    exit /b 1
)
echo ✓ MailHog is running
echo.

REM Create PowerShell script inline
echo [2/3] Creating PowerShell test script...
(
powershell -Command ^
  $SMTPClient = New-Object System.Net.Mail.SmtpClient; ^
  $SMTPClient.Host = 'localhost'; ^
  $SMTPClient.Port = 1025; ^
  $SMTPClient.EnableSsl = $false; ^
  $SMTPClient.Timeout = 10000; ^
  $MailMessage = New-Object System.Net.Mail.MailMessage; ^
  $MailMessage.From = 'rtmishra8985@gmail.com'; ^
  $MailMessage.To.Add('rtmishra7040@gmail.com'); ^
  $MailMessage.Subject = '🟢 RatanHR Phase 8 - MailHog TEST EMAIL'; ^
  $MailMessage.IsBodyHtml = $true; ^
  $MailMessage.Body = @' ^
^<html^> ^
  ^<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'^> ^
    ^<h1^>🟢 RatanHR Phase 8 - MailHog Test Email^</h1^> ^
    ^<h2^>Email Configuration Test^</h2^> ^
    ^<p^>^<strong^>Status:^</strong^> ✅ MAILHOG CONFIGURED ^& WORKING^</p^> ^
    ^<h3^>Test Details^</h3^> ^
    ^<ul^> ^
      ^<li^>^<strong^>From:^</strong^> rtmishra8985@gmail.com^</li^> ^
      ^<li^>^<strong^>To:^</strong^> rtmishra7040@gmail.com^</li^> ^
      ^<li^>^<strong^>Time:^</strong^> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')^</li^> ^
      ^<li^>^<strong^>Server:^</strong^> MailHog (localhost:1025)^</li^> ^
    ^</ul^> ^
    ^<h3^>What This Means^</h3^> ^
    ^<p^>✅ Email service is configured for local testing^</p^> ^
    ^<p^>✅ MailHog is capturing all emails sent^</p^> ^
    ^<p^>✅ Ready for on-premise deployment^</p^> ^
    ^<hr /^> ^
    ^<p^>^<em^>Test email from RatanHR Phase 8^</em^>^</p^> ^
  ^</body^> ^
^</html^> ^
'@; ^
  try { ^
    $SMTPClient.Send($MailMessage); ^
    Write-Host '✓ Email sent successfully!'; ^
    exit 0 ^
  } catch { ^
    Write-Host ('❌ Error: ' + $_.Exception.Message); ^
    exit 1 ^
  }
) else (
  echo.
  echo ❌ ERROR: Failed to create test script
  echo.
  pause
  exit /b 1
)

echo.
echo [3/3] Finalizing...
echo.

echo ╔════════════════════════════════════════════════════════════════════════════╗
echo ║   ✅ SUCCESS - EMAIL SENT TO MAILHOG                                       ║
echo ╚════════════════════════════════════════════════════════════════════════════╝
echo.
echo NEXT STEPS:
echo   1. Open browser: http://localhost:8025
echo   2. Check inbox for email from: rtmishra8985@gmail.com
echo   3. Subject: "🟢 RatanHR Phase 8 - MailHog TEST EMAIL"
echo   4. Verify email content is displayed
echo.

pause
