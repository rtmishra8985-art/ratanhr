using BCrypt.Net;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Security;

/// <summary>
/// Centralizes BCrypt configuration so password creation always uses the
/// deployment-configured work factor while existing hashes remain verifiable.
/// </summary>
public static class BcryptPasswordHasher
{
    public const int DefaultWorkFactor = 12;
    public const string ConfigurationKey = "Security:BcryptWorkFactor";

    public static string Hash(string password, IConfiguration configuration)
    {
        var workFactor = configuration.GetValue(ConfigurationKey, DefaultWorkFactor);
        if (workFactor is < 4 or > 31)
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be between 4 and 31.");

        return BCrypt.Net.BCrypt.HashPassword(password, workFactor);
    }
}