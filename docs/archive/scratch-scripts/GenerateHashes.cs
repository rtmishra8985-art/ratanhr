using BCrypt.Net;

// Generate real BCrypt hashes for test passwords
var passwords = new[] { "test123", "Admin123@", "SuperAdmin@2026", "Password@123" };

foreach (var pwd in passwords)
{
    var hash = BCrypt.Net.BCrypt.HashPassword(pwd, 12);
    Console.WriteLine($"Password: {pwd}");
    Console.WriteLine($"Hash: {hash}");
    
    // Verify it works
    bool valid = BCrypt.Net.BCrypt.Verify(pwd, hash);
    Console.WriteLine($"Verify: {valid}");
    Console.WriteLine();
}
