@echo off
echo.
echo ╔════════════════════════════════════════════════════════╗
echo ║   RatanHR Phase 8 - Brevo SMTP Test Email Sender      ║
echo ╚════════════════════════════════════════════════════════╝
echo.

cd /d C:\Users\karun\Downloads\RatanHR_Run8_Final\RatanHR

echo [+] Compiling C# email sender...
csc SendTestEmail.cs 2>nul

if errorlevel 1 (
    echo [X] Compilation failed
    echo.
    echo Attempting alternative method...
    echo.
    
    REM Try using dotnet if available
    dotnet --version >nul 2>&1
    if errorlevel 1 (
        echo [X] .NET SDK not found
        echo Please install .NET SDK from: https://dotnet.microsoft.com/download
        echo.
        pause
        exit /b 1
    )
)

echo [+] Running email sender...
echo.

SendTestEmail.exe

if errorlevel 1 (
    echo.
    echo [X] Email sending failed
    echo.
    pause
    exit /b 1
)

echo.
echo ✅ TEST COMPLETED SUCCESSFULLY
echo.
echo Check rtmishra7040@gmail.com for the test email
echo.
pause
