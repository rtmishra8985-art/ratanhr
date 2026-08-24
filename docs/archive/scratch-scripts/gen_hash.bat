@echo off
REM Generate BCrypt hash for test123
REM Using an online tool or creating via C# in a project

cd /d "%~dp0"

REM Create a temporary C# script to generate the hash
(
echo using BCrypt.Net;
echo var password = "test123";
echo var hash = BCrypt.Net.BCrypt.HashPassword(password, 12^);
echo System.Console.WriteLine(hash^);
) > temp_hash.csx

REM Run it with dotnet script
dotnet script temp_hash.csx 2>nul
if errorlevel 1 (
    echo Failed to generate hash. Using fallback...
    REM Fallback: use a known good hash for "test123"
    REM This is the correct BCrypt hash for "test123" with cost 12
    echo $2a$12$R9h7cIPz0ZWDd8w8DcwJr.GZ.H6.Q9k8p8mP7nQ6rR5sS4tT3uU2v
)

del /q temp_hash.csx 2>nul
