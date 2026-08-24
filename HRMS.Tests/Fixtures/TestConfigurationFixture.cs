using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace HRMS.Tests.Fixtures;

/// <summary>
/// Provides a shared, test-only RSA key pair and IConfiguration for tests
/// that require JWT, encryption, or startup-validation configuration.
/// Keys are generated fresh for each test run and never committed to source.
/// </summary>
public static class TestConfigurationFixture
{
    private static readonly RSA _rsa;
    private static readonly string _privateKeyPem;
    private static readonly string _publicKeyPem;
    private static readonly string _encryptionKey;

    static TestConfigurationFixture()
    {
        _rsa = RSA.Create(2048);
        _privateKeyPem  = _rsa.ExportRSAPrivateKeyPem();
        _publicKeyPem   = _rsa.ExportRSAPublicKeyPem();
        _encryptionKey  = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public static string PrivateKeyPem  => _privateKeyPem;
    public static string PublicKeyPem   => _publicKeyPem;
    public static string EncryptionKey  => _encryptionKey;

    /// <summary>
    /// Returns a fully-populated IConfiguration suitable for any service
    /// that depends on Jwt, Security, Compliance, or ConnectionStrings settings.
    /// </summary>
    public static IConfiguration Build(
        Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyPem"]             = _privateKeyPem,
            ["Jwt:PublicKeyPem"]              = _publicKeyPem,
            ["Jwt:Issuer"]                    = "HRMS.API",
            ["Jwt:Audience"]                  = "HRMS.Client",
            ["Jwt:ExpiresInMinutes"]          = "30",
            ["Jwt:RefreshTokenExpiryDays"]    = "7",
            ["Security:EncryptionKey"]        = _encryptionKey,
            ["Compliance:DpoEmail"]           = "dpo@test.example.com",
            ["Compliance:ComplianceRegime"]   = "dpdp",
            ["AllowedHosts"]                  = "*",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Port=3306;Database=hrms_test;" +
                 "User ID=hrms;Password=testpass;AllowPublicKeyRetrieval=True;SslMode=Required;"
        };

        if (overrides != null)
            foreach (var kv in overrides)
                defaults[kv.Key] = kv.Value;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }
}
