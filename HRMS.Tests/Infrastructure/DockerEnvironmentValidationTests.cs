// Pure unit tests for EnvironmentValidator.Validate() — no Docker, no live services,
// no HTTP server. IConfiguration is built inline via ConfigurationBuilder so tests
// are fully self-contained and run in any environment.
//
// Three scenarios:
//   1. Jwt__PublicKeyPem missing in Production  → throws InvalidOperationException.
//   2. Jwt__PublicKeyPem present in Production  → does not throw (all other required
//      keys are supplied via ValidProdEntries()).
//   3. Security:EncryptionKey absent in Production  → throws;
//      Security:EncryptionKey absent in Development → does not throw.

using HRMS.API.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text;
using Xunit;

namespace HRMS.Tests.Infrastructure;

/// <summary>
/// Unit tests that drive <see cref="EnvironmentValidator.Validate"/> directly
/// using inline <see cref="IConfiguration"/> instances.  No Docker, no live
/// infrastructure, and no NuGet packages beyond what HRMS.Tests already references.
/// </summary>
public class DockerEnvironmentValidationTests
{
    // ── RSA key pair generated once per class ─────────────────────────────────
    // Reuses the same test helper as StartupValidationTests so key generation
    // is not duplicated across test classes.
    private static readonly (string Priv, string Pub) Keys =
        TestHelpers.GenerateTestRsaKeyPair();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IWebHostEnvironment MakeEnv(string name)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    private static IWebHostEnvironment Production() => MakeEnv("Production");
    private static IWebHostEnvironment Development() => MakeEnv("Development");

    /// <summary>
    /// Returns a configuration dictionary that satisfies every production
    /// requirement, so individual tests can remove one key at a time.
    /// </summary>
    private static Dictionary<string, string?> ValidProdEntries() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=hrms;Username=pg;Password=test",
        ["Jwt:PrivateKeyPem"]                   = Keys.Priv,
        ["Jwt:PublicKeyPem"]                    = Keys.Pub,
        ["Jwt:Issuer"]                          = "HRMS.API",
        ["Jwt:Audience"]                        = "HRMS.Client",
        ["Security:EncryptionKey"]              = Convert.ToBase64String(
                                                     Encoding.UTF8.GetBytes(new string('k', 32))),
        ["AllowedHosts"]                        = "hrms.test",
        ["Cors:AllowedOrigins"]                 = "https://hrms.test",
        ["Redis:ConnectionString"]              = "localhost:6379,password=test",
        // EnvironmentValidator requires DpoEmail in non-Development environments.
        ["Compliance:DpoEmail"]                 = "dpo@hrms.test",
        ["Compliance:ComplianceRegime"]         = "dpdp"
    };

    private static IConfiguration BuildCfg(Dictionary<string, string?> entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(entries).Build();

    // ── Test 1: Jwt__PublicKeyPem missing in Production → throws ─────────────

    /// <summary>
    /// When <c>Jwt:PublicKeyPem</c> is absent and the environment is Production,
    /// <see cref="EnvironmentValidator.Validate"/> must throw
    /// <see cref="InvalidOperationException"/> with a message referencing PublicKeyPem.
    /// </summary>
    [Fact]
    public void JwtPublicKeyPem_MissingInProduction_ThrowsInvalidOperationException()
    {
        var config = ValidProdEntries();
        config["Jwt:PublicKeyPem"] = null;   // remove the public key

        var ex = Assert.Throws<InvalidOperationException>(
            () => EnvironmentValidator.Validate(BuildCfg(config), Production()));

        Assert.Contains("PublicKeyPem", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 2: Jwt__PublicKeyPem present in Production → does not throw ─────

    /// <summary>
    /// When all required keys including <c>Jwt:PublicKeyPem</c> are present in
    /// Production, <see cref="EnvironmentValidator.Validate"/> must not throw.
    /// </summary>
    [Fact]
    public void JwtPublicKeyPem_PresentInProduction_DoesNotThrow()
    {
        // ValidProdEntries() includes a valid PEM — no exception expected.
        var config = ValidProdEntries();

        // Must complete without exception.
        EnvironmentValidator.Validate(BuildCfg(config), Production());
    }

    // ── Test 3: Security:EncryptionKey absent — different behaviour per env ───

    /// <summary>
    /// Absence of <c>Security:EncryptionKey</c> must throw in Production
    /// (PII at rest would be unencrypted) but must NOT throw in Development
    /// (local development runs without encryption for convenience).
    /// </summary>
    [Fact]
    public void EncryptionKey_AbsentInProduction_ThrowsButNotInDevelopment()
    {
        // ── Production: must throw ─────────────────────────────────────────────
        var prodConfig = ValidProdEntries();
        prodConfig["Security:EncryptionKey"] = null;

        var ex = Assert.Throws<InvalidOperationException>(
            () => EnvironmentValidator.Validate(BuildCfg(prodConfig), Production()));
        Assert.Contains("EncryptionKey", ex.Message, StringComparison.OrdinalIgnoreCase);

        // ── Development: must NOT throw ────────────────────────────────────────
        // Development environment relaxes CORS, AllowedHosts, and EncryptionKey checks.
        var devConfig = ValidProdEntries();
        devConfig["Security:EncryptionKey"] = null;
        devConfig["AllowedHosts"]           = "*";   // dev wildcard is allowed
        devConfig["Cors:AllowedOrigins"]    = null;  // optional in dev

        // Must complete without exception.
        EnvironmentValidator.Validate(BuildCfg(devConfig), Development());
    }

    [Fact]
    public void LegacyEnvironmentSecretNames_AreAcceptedForCompatibility()
    {
        var values = ValidProdEntries();
        values.Remove("Jwt:PrivateKeyPem");
        values.Remove("Jwt:PublicKeyPem");
        values.Remove("Security:EncryptionKey");
        values["JWT_PRIVATE_KEY_PEM"] = Keys.Priv;
        values["JWT_PUBLIC_KEY_PEM"] = Keys.Pub;
        values["ENCRYPTION_KEY"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(new string('k', 32)));

        EnvironmentValidator.Validate(BuildCfg(values), Production());
    }
}
