#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

using BCrypt.Net;

string password = "Password@123";
string hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

Console.WriteLine($"Password: {password}");
Console.WriteLine($"Hash: {hash}");

// Verify
bool ok = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Verify: {ok}");

// Output SQL
Console.WriteLine($"\nSQL: UPDATE hrms_db.users SET password_hash='{hash}' WHERE email='superadmin@hrms.com';");
