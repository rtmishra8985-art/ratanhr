using System;
using BCrypt.Net;

class Program
{
    static void Main()
    {
        string password = "SuperAdmin@2026";
        string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        Console.WriteLine($"Password: {password}");
        Console.WriteLine($"BCrypt Hash (cost 12): {hash}");
        
        // Verify
        bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
        Console.WriteLine($"Verification: {(isValid ? "✓ PASS" : "✗ FAIL")}");
    }
}
