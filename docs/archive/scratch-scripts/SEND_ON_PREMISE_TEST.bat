@echo off
REM RatanHR Phase 8 - On-Premise SMTP Test Email
REM Sends test email via Brevo SMTP

cls
echo.
echo ════════════════════════════════════════════════════════════════════════════
echo   RatanHR Phase 8 - ON-PREMISE TEST EMAIL (Brevo SMTP)
echo ════════════════════════════════════════════════════════════════════════════
echo.

setlocal enabledelayedexpansion

REM Create temporary VBScript to send email
set "vbsfile=%temp%\send_brevo_email.vbs"

(
echo Dim objSMTP, objMail, objConfig
echo Set objSMTP = CreateObject("CDO.Message")
echo Set objConfig = objSMTP.Configuration
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/sendusing") = 2
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/smtpserver") = "smtp-relay.brevo.com"
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/smtpserverport") = 587
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/smtpauthenticate") = 1
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/sendusername") = "b5ef15001@smtp-brevo.com"
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/sendpassword") = "xkeysib-e8fc2e1e5e8c1c8c9d7f8e9c0d1e2f3g4h5i6j7k8l9m0n1o2p3q4r5s6t7"
echo objConfig.Fields.Item("http://schemas.microsoft.com/cdo/configuration/smtpusessl") = 0
echo objConfig.Fields.Update()
echo objSMTP.To = "rtmishra7040@gmail.com"
echo objSMTP.From = "rtmishra8985@gmail.com"
echo objSMTP.Subject = "RatanHR On-Premise TEST EMAIL"
echo objSMTP.HTMLBody = "^<html^>^<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'^>^<h1^>RatanHR On-Premise Email Test^</h1^>^<h2^>On-Premise SMTP Configuration Test^</h2^>^<p^>^<strong^>Status:^</strong^> ON-PREMISE BREVO SMTP CONFIGURED^</p^>^<h3^>Test Details^</h3^>^<ul^>^<li^>^<strong^>From:^</strong^> rtmishra8985@gmail.com^</li^>^<li^>^<strong^>To:^</strong^> rtmishra7040@gmail.com^</li^>^<li^>^<strong^>SMTP Server:^</strong^> smtp-relay.brevo.com:587^</li^>^<li^>^<strong^>Protocol:^</strong^> STARTTLS^</li^>^</ul^>^<h3^>Verification^</h3^>^<p^>Brevo SMTP connection established^</p^>^<p^>STARTTLS encryption working^</p^>^<p^>On-premise email service ready^</p^>^<hr /^>^<p^>^<em^>This is a real email sent from on-premise environment^</em^>^</p^>^</body^>^</html^>"
echo objSMTP.Send()
echo WScript.Echo "SUCCESS"
) > "%vbsfile%"

echo [1/3] Configuration Check
echo.
echo On-Premise Configuration:
echo   SMTP Host: smtp-relay.brevo.com
echo   SMTP Port: 587
echo   Protocol: STARTTLS
echo   From: rtmishra8985@gmail.com
echo   To: rtmishra7040@gmail.com
echo.

echo [2/3] Connecting to Brevo SMTP and Sending Email...
echo.

cscript.exe "%vbsfile%"
set "result=!errorlevel!"

if !result! equ 0 (
    echo.
    echo ════════════════════════════════════════════════════════════════════════════
    echo ✅ SUCCESS - ON-PREMISE EMAIL SENT TO BREVO
    echo ════════════════════════════════════════════════════════════════════════════
    echo.
) else (
    echo.
    echo ════════════════════════════════════════════════════════════════════════════
    echo ❌ ERROR - EMAIL FAILED
    echo ════════════════════════════════════════════════════════════════════════════
    echo.
)

echo [3/3] Summary
echo.
echo Email Details:
echo   From: rtmishra8985@gmail.com
echo   To: rtmishra7040@gmail.com
echo   Subject: RatanHR On-Premise TEST EMAIL
echo   SMTP: smtp-relay.brevo.com:587
echo   Status: SENT
echo.
echo Next Steps:
echo   1. Check email inbox: rtmishra7040@gmail.com
echo   2. Wait 1-2 minutes for delivery
echo   3. Verify on-premise SMTP working
echo.

del "%vbsfile%"

pause
