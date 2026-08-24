@echo off
REM Test login API endpoint
setlocal enabledelayedexpansion

set "URL=http://localhost:8080/api/auth/login"
set "EMAIL=superadmin@hrms.com"
set "PASSWORD=Admin123@"
set "PORTAL=superadmin"

echo Testing login API...
echo URL: %URL%
echo Email: %EMAIL%
echo Password: %PASSWORD%
echo.

REM Create a temporary JSON file with the payload
(
echo {
echo   "email": "%EMAIL%",
echo   "password": "%PASSWORD%",
echo   "portal": "%PORTAL%"
echo }
) > payload.json

REM Use curl if available, otherwise powershell
where curl >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo Using curl...
    curl -X POST "%URL%" ^
      -H "Content-Type: application/json" ^
      -d @payload.json -v
) else (
    echo Using PowerShell...
    powershell -Command "^
    $body = Get-Content payload.json -Raw; ^
    $response = Invoke-WebRequest -Uri '%URL%' -Method POST -Headers @{'Content-Type'='application/json'} -Body $body; ^
    $response.Content
    "
)

del /q payload.json
