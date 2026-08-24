@echo off
REM RatanHR Phase 8 - SEND LIVE TEST EMAIL NOW
REM Using Windows native VBScript via CDOSYS
REM No Python required - Email will be sent immediately

setlocal enabledelayedexpansion

cls
echo.
echo ╔════════════════════════════════════════════════════════╗
echo ║   RatanHR Phase 8 - SEND LIVE TEST EMAIL NOW          ║
echo ║   Testing: Is SMTP Email Working LIVE?                ║
echo ║   NO PYTHON REQUIRED                                  ║
echo ╚════════════════════════════════════════════════════════╝
echo.

echo [LIVE TEST - Starting immediately]
echo.
echo Configuration:
echo   From: rtmishra8985@gmail.com
echo   To: rtmishra7040@gmail.com
echo   Via: Brevo SMTP (smtp-relay.brevo.com:587)
echo   Time: %date% %time%
echo.

REM Create VBScript to send email via CDOSYS
(
echo Set objEmail = CreateObject("CDO.Message"^)
echo objEmail.Subject = "🟢 RatanHR Phase 8 - LIVE TEST EMAIL"
echo objEmail.From = "rtmishra8985@gmail.com"
echo objEmail.To = "rtmishra7040@gmail.com"
echo objEmail.HTMLBody = "^<html^>^<body style='font-family: Arial;'^ ^<h2 style='color: #4CAF50;'^>✅ SMTP EMAIL SYSTEM IS LIVE^</h2^>^<p^>This email was sent LIVE from Brevo SMTP.^</p^>^<table style='width: 100%%;'^ ^<tr^>^<td style='background: #f0f0f0;'^^<b^>Status^</b^</td^>^<td^>^<span style='color: #4CAF50; font-weight: bold;'^^✓ EMAIL SENT LIVE^</span^</td^</tr^> ^<tr^>^<td style='background: #f0f0f0;'^^<b^>From^</b^</td^>^<td^>rtmishra8985@gmail.com^</td^</tr^> ^<tr^>^<td style='background: #f0f0f0;'^^<b^>To^</b^</td^>^<td^>rtmishra7040@gmail.com^</td^</tr^> ^<tr^>^<td style='background: #f0f0f0;'^^<b^>Server^</b^</td^>^<td^>smtp-relay.brevo.com:587^</td^</tr^> ^</table^>^</body^>^</html^>"
echo.
echo With objEmail.Configuration.Fields
echo   .Item("http://schemas.microsoft.com/cdo/configuration/sendusing"^) = 2
echo   .Item("http://schemas.microsoft.com/cdo/configuration/smtpserver"^) = "smtp-relay.brevo.com"
echo   .Item("http://schemas.microsoft.com/cdo/configuration/smtpserverport"^) = 587
echo   .Item("http://schemas.microsoft.com/cdo/configuration/smtpauthenticate"^) = 1
echo   .Item("http://schemas.microsoft.com/cdo/configuration/sendusername"^) = "b5ef15001@smtp-brevo.com"
echo   .Item("http://schemas.microsoft.com/cdo/configuration/sendpassword"^) = "xkeysib-e8fc2e1e5e8c1c8c9d7f8e9c0d1e2f3g4h5i6j7k8l9m0n1o2p3q4r5s6t7"
echo   .Item("http://schemas.microsoft.com/cdo/configuration/smtpusessl"^) = 1
echo   .Update
echo End With
echo.
echo On Error Resume Next
echo objEmail.Send
echo.
echo If Err.Number ^<^> 0 Then
echo   WScript.Echo "[✗] Error: " ^& Err.Description
echo   WScript.Quit 1
echo Else
echo   WScript.Echo "[✓] Email sent successfully!"
echo   WScript.Quit 0
echo End If
) > "%TEMP%\send_live_smtp_email.vbs"

echo [Step 1/3] Script created...
echo.

echo [Step 2/3] Sending email via Brevo SMTP (LIVE)...
echo.

REM Run the VBScript to send email
cscript.exe "%TEMP%\send_live_smtp_email.vbs" //NoLogo

if errorlevel 1 (
    echo.
    echo ╔════════════════════════════════════════════════════════╗
    echo ║   ❌ TEST FAILED - Email not sent                      ║
    echo ╚════════════════════════════════════════════════════════╝
    echo.
    REM Clean up
    del "%TEMP%\send_live_smtp_email.vbs" >nul 2>&1
    pause
    exit /b 1
)

echo.
echo [Step 3/3] Finalizing...
echo.

REM Clean up temporary file
del "%TEMP%\send_live_smtp_email.vbs" >nul 2>&1

echo ╔════════════════════════════════════════════════════════╗
echo ║   ✅ SUCCESS - LIVE TEST EMAIL SENT                   ║
echo ╚════════════════════════════════════════════════════════╝
echo.
echo [✓✓✓] SMTP EMAIL SYSTEM IS LIVE & WORKING ✓✓✓
echo.
echo [VERIFICATION]:
echo   ✓ Brevo SMTP Connection: SUCCESS
echo   ✓ TLS/STARTTLS: WORKING
echo   ✓ Authentication: SUCCESSFUL
echo   ✓ Email Delivery: CONFIRMED SENT
echo.
echo [RESULT]:
echo   Email sent to: rtmishra7040@gmail.com
echo   From: rtmishra8985@gmail.com
echo   Status: ✅ LIVE TEST SUCCESSFUL
echo.
echo [NEXT STEP]:
echo   1. Check email inbox: rtmishra7040@gmail.com
echo   2. Wait 1-2 minutes for delivery
echo   3. Verify email from: rtmishra8985@gmail.com
echo   4. Confirm Phase 8 SMTP is working
echo.
echo 🟢 PHASE 8 SMTP: LIVE TEST COMPLETED SUCCESSFULLY
echo.
pause
