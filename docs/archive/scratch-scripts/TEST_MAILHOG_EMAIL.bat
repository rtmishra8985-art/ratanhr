@echo off
cls
setlocal enabledelayedexpansion

echo.
echo ╔════════════════════════════════════════════════════════════════════════════╗
echo ║   RatanHR Phase 8 - MailHog SMTP Test Email                               ║
echo ║   Testing: Is MailHog Capturing Emails?                                   ║
echo ╚════════════════════════════════════════════════════════════════════════════╝
echo.

REM Check if Python is installed
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ ERROR: Python is not installed or not in PATH
    echo.
    echo Solutions:
    echo   1. Install Python from https://python.org
    echo   2. During install, check "Add Python to PATH"
    echo   3. Restart Command Prompt
    echo.
    pause
    exit /b 1
)

echo ✓ Python found
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
    echo   1. Download from: https://github.com/mailhog/MailHog/releases
    echo   2. Run: mailhog_windows_amd64.exe
    echo   3. Wait for: "Binding to address: 0.0.0.0:1025"
    echo   4. Then run this script again
    echo.
    pause
    exit /b 1
)
echo ✓ MailHog is running
echo.

REM Run Python test script
echo [2/3] Running test script...
echo.
python test_mailhog_email.py
set RESULT=%errorlevel%

echo.
echo [3/3] Finalizing...
echo.

if %RESULT% equ 0 (
    echo ╔════════════════════════════════════════════════════════════════════════════╗
    echo ║   ✅ SUCCESS - EMAIL SENT TO MAILHOG                                       ║
    echo ╚════════════════════════════════════════════════════════════════════════════╝
    echo.
    echo NEXT STEPS:
    echo   1. Open browser: http://localhost:8025
    echo   2. Check inbox for email from: rtmishra8985@gmail.com
    echo   3. Subject: "🟢 RatanHR Phase 8 - MailHog TEST EMAIL"
    echo.
) else (
    echo ╔════════════════════════════════════════════════════════════════════════════╗
    echo ║   ❌ ERROR - EMAIL FAILED                                                  ║
    echo ╚════════════════════════════════════════════════════════════════════════════╝
    echo.
)

echo.
pause
