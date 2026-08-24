using BCrypt.Net;
using System;

// Generate a proper BCrypt hash
string password = "SuperAdmin@2026";

// Use cost factor 12 as defined in BcryptPasswordHasher
string hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

Console.WriteLine($"Password: {password}");
Console.WriteLine($"BCrypt Hash: {hash}");

// Test verification
bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
Console.WriteLine($"Verify Test: {(isValid ? "✓ PASS" : "✗ FAIL")}");

// Output the SQL statement
Console.WriteLine();
Console.WriteLine("SQL Update Statement:");
Console.WriteLine($"UPDATE hrms_db.users SET password_hash='{hash}' WHERE email='superadmin@hrms.com';");
