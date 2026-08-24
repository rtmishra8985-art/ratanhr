@echo off
cls

echo.
echo ════════════════════════════════════════════════════════════════════════════
echo   RatanHR Phase 8 - MailHog SMTP Test Email
echo ════════════════════════════════════════════════════════════════════════════
echo.

REM Check if MailHog is running on port 1025
echo [1/2] Checking if MailHog is running...
netstat -ano | findstr ":1025" >nul 2>&1

if %errorlevel% neq 0 (
    echo.
    echo ❌ MailHog is NOT running on port 1025
    echo.
    echo To fix:
    echo   1. Download: mailhog_windows_amd64.exe
    echo   2. From: https://github.com/mailhog/MailHog/releases
    echo   3. Run it (keep the window open)
    echo   4. Then run this script again
    echo.
    pause
    exit /b 1
)

echo ✓ MailHog is running on localhost:1025
echo.

REM Use PowerShell to send email via MailHog
echo [2/2] Sending test email...
echo.

powershell -NoProfile -Command ^
  "try { ^
    $smtp = New-Object System.Net.Mail.SmtpClient('localhost', 1025); ^
    $smtp.EnableSsl = $false; ^
    $msg = New-Object System.Net.Mail.MailMessage('rtmishra8985@gmail.com', 'rtmishra7040@gmail.com'); ^
    $msg.Subject = '🟢 RatanHR Phase 8 - MailHog TEST EMAIL'; ^
    $msg.IsBodyHtml = $true; ^
    $msg.Body = @' ^
^<html^>^<body style=^'font-family: Arial; color: #333;'^> ^
^<h1^>🟢 RatanHR Phase 8 - MailHog Test Email^</h1^> ^
^<p^>✅ Email successfully sent to MailHog^</p^> ^
^<p^>Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')^</p^> ^
^<p^>Server: MailHog (localhost:1025)^</p^> ^
^</body^>^</html^> ^
'@; ^
    $smtp.Send($msg); ^
    Write-Host '✓ Email sent successfully!'; ^
    Write-Host ''; ^
    Write-Host '✅ SUCCESS - EMAIL SENT TO MAILHOG'; ^
  } catch { ^
    Write-Host '❌ Error: ' $_.Exception.Message; ^
  }"

echo.
echo ════════════════════════════════════════════════════════════════════════════
echo.
echo NEXT: Open your browser and go to: http://localhost:8025
echo.
echo You should see the test email in MailHog inbox from:
echo   From: rtmishra8985@gmail.com
echo   To: rtmishra7040@gmail.com
echo   Subject: 🟢 RatanHR Phase 8 - MailHog TEST EMAIL
echo.

pause
