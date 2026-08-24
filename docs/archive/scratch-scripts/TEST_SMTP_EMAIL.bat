@echo off
REM RatanHR Phase 8 - SMTP Email Test Runner
REM Tests if Brevo SMTP email is working

cls
echo.
echo ╔════════════════════════════════════════════════════════╗
echo ║   RatanHR Phase 8 - SMTP Email Test                  ║
echo ║   Testing: Is SMTP Email Working?                     ║
echo ╚════════════════════════════════════════════════════════╝
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo [X] Python not found
    echo.
    echo Solution: Install Python from https://python.org
    echo Or run the C# version instead
    echo.
    pause
    exit /b 1
)

echo [+] Python found
echo.
echo [→] Running SMTP test...
echo.

cd /d C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR

python test_smtp_email_now.py

if errorlevel 1 (
    echo.
    echo ❌ TEST FAILED
    echo.
    echo Troubleshooting:
    echo - Check internet connection
    echo - Verify Brevo SMTP credentials
    echo - Check firewall (port 587)
    echo.
    pause
    exit /b 1
)

echo.
echo ✅ TEST COMPLETED
echo.
echo Next steps:
echo 1. Check rtmishra7040@gmail.com inbox
echo 2. Wait 1-2 minutes for email delivery
echo 3. Confirm email received with Phase 8 verification details
echo.
pause
