#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

using BCrypt.Net;

string password = "SuperAdmin@2026";
string hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

Console.WriteLine($"Password: {password}");
Console.WriteLine($"BCrypt Hash: {hash}");

// Verify it works
bool valid = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Verification: {valid}");

// Output for SQL
Console.WriteLine($"\nFor MySQL UPDATE:");
Console.WriteLine($"UPDATE users SET password_hash='{hash}' WHERE email='superadmin@hrms.com';");
