using HRMS.API.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for <see cref="EnvironmentValidator"/>.
///
/// Phase 2 coverage (P2-B ALLOWED_HOSTS, P2-C Hangfire Redis):
///   ✓ Missing ALLOWED_HOSTS fails startup.
///   ✓ Empty ALLOWED_HOSTS fails startup.
///   ✓ Wildcard * fails startup.
///   ✓ REPLACE_WITH_PRODUCTION_HOSTS placeholder fails startup.
///   ✓ Example domains fail startup.
///   ✓ Valid configured hosts allow startup.
///   ✓ Multiple valid hosts (semicolon-separated) allow startup.
///   ✓ Wildcard subdomain prefix (.domain.com) is allowed.
///   ✓ Missing Redis config fails startup in Production.
///   ✓ Hangfire:UseRedis=false fails startup in Production.
///   ✓ Empty Redis connection string fails startup in Production.
///   ✓ No Redis config in Development does NOT fail startup.
///   ✓ No Redis config in Test environment does NOT fail startup.
///   ✓ Missing required secrets fail startup.
/// </summary>
public class StartupValidationTests
{
    // ── ALLOWED_HOSTS — failure cases ─────────────────────────────────────────

    [Fact]
    public void Validate_MissingAllowedHosts_ThrowsInProduction()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["JWT_PRIVATE_KEY_PEM"] = "pem",
            ["JWT_PUBLIC_KEY_PEM"]  = "pem",
            ["ENCRYPTION_KEY"]      = "key",
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test",
            ["Hangfire:UseRedis"]                  = "true",
            ["Hangfire:RedisConnectionString"]      = "localhost:6379,password=x",
            // ALLOWED_HOSTS deliberately absent
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("ALLOWED_HOSTS", ex.Message);
    }

    [Fact]
    public void Validate_EmptyAllowedHosts_ThrowsInProduction()
    {
        var config = BuildConfig(BaseProductionValues(allowedHosts: ""));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("ALLOWED_HOSTS", ex.Message);
    }

    [Fact]
    public void Validate_WildcardAllowedHosts_ThrowsInProduction()
    {
        var config = BuildConfig(BaseProductionValues(allowedHosts: "*"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("wildcard", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_PlaceholderAllowedHosts_ThrowsInProduction()
    {
        var config = BuildConfig(BaseProductionValues(allowedHosts: "REPLACE_WITH_PRODUCTION_HOSTS"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("example.org")]
    [InlineData("example.net")]
    [InlineData("api.example.com")]
    [InlineData("yourcompany.com")]
    public void Validate_ExampleDomains_ThrowsInProduction(string badHost)
    {
        var config = BuildConfig(BaseProductionValues(allowedHosts: badHost));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── ALLOWED_HOSTS — success cases ─────────────────────────────────────────

    [Fact]
    public void Validate_ValidSingleHost_DoesNotThrow()
    {
        var config = BuildConfig(BaseProductionValues(allowedHosts: "api.acme-corp.io"));
        EnvironmentValidator.Validate(config, ProductionEnv());
    }

    [Fact]
    public void Validate_MultipleValidHosts_SemicolonSeparated_DoesNotThrow()
    {
        var config = BuildConfig(BaseProductionValues(allowedHosts: "api.acme-corp.io;acme-corp.io"));
        EnvironmentValidator.Validate(config, ProductionEnv());
    }

    [Fact]
    public void Validate_WildcardSubdomainPrefix_DoesNotThrow()
    {
        // ASP.NET Core allows ".acme-corp.io" to match all subdomains.
        var config = BuildConfig(BaseProductionValues(allowedHosts: ".acme-corp.io"));
        EnvironmentValidator.Validate(config, ProductionEnv());
    }

    // ── Hangfire / Redis — failure cases ──────────────────────────────────────

    [Fact]
    public void Validate_HangfireUseRedisFalse_ThrowsInProduction()
    {
        var values = BaseProductionValues("api.acme-corp.io");
        values["Hangfire:UseRedis"] = "false";
        var config = BuildConfig(values);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("Hangfire:UseRedis", ex.Message);
    }

    [Fact]
    public void Validate_MissingHangfireRedisConnectionString_ThrowsInProduction()
    {
        var values = BaseProductionValues("api.acme-corp.io");
        values.Remove("Hangfire:RedisConnectionString");
        var config = BuildConfig(values);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("RedisConnectionString", ex.Message);
    }

    [Fact]
    public void Validate_EmptyHangfireRedisConnectionString_ThrowsInProduction()
    {
        var values = BaseProductionValues("api.acme-corp.io");
        values["Hangfire:RedisConnectionString"] = "";
        var config = BuildConfig(values);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("RedisConnectionString", ex.Message);
    }

    // Failing-first audit coverage: in-memory Hangfire storage must not be
    // allowed in a non-Development environment, even when Redis is configured.
    [Fact]
    public void Validate_HangfireUseInMemory_ThrowsOutsideDevelopment()
    {
        var values = BaseProductionValues("api.acme-corp.io");
        values["Hangfire:UseInMemory"] = "true";
        var config = BuildConfig(values);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains("UseInMemory", ex.Message);
    }

    // ── Hangfire / Redis — Development / Test allow in-memory ─────────────────

    [Fact]
    public void Validate_NoRedisConfig_DoesNotThrowInDevelopment()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["JWT_PRIVATE_KEY_PEM"] = "pem",
            ["JWT_PUBLIC_KEY_PEM"]  = "pem",
            ["ENCRYPTION_KEY"]      = "key",
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test",
        });

        EnvironmentValidator.Validate(config, DevelopmentEnv());
    }

    [Fact]
    public void Validate_NoRedisConfig_DoesNotThrowInTestEnvironment()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["JWT_PRIVATE_KEY_PEM"] = "pem",
            ["JWT_PUBLIC_KEY_PEM"]  = "pem",
            ["ENCRYPTION_KEY"]      = "key",
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test",
        });

        EnvironmentValidator.Validate(config, NamedEnv("Test"));
    }

    // ── Required secrets ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("JWT_PRIVATE_KEY_PEM")]
    [InlineData("JWT_PUBLIC_KEY_PEM")]
    [InlineData("ENCRYPTION_KEY")]
    [InlineData("ConnectionStrings:DefaultConnection")]
    public void Validate_MissingRequiredSecret_Throws(string missingKey)
    {
        var values = BaseProductionValues("api.acme-corp.io");
        values.Remove(missingKey);
        var config = BuildConfig(values);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnvironmentValidator.Validate(config, ProductionEnv()));

        Assert.Contains(missingKey, ex.Message);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static HostingEnvironment ProductionEnv()   => NamedEnv("Production");
    private static HostingEnvironment DevelopmentEnv()  => NamedEnv("Development");

    private static HostingEnvironment NamedEnv(string name) =>
        new() { EnvironmentName = name };

    private static Dictionary<string, string?> BaseProductionValues(string allowedHosts) =>
        new()
        {
            ["JWT_PRIVATE_KEY_PEM"]  = "pem",
            ["JWT_PUBLIC_KEY_PEM"]   = "pem",
            ["ENCRYPTION_KEY"]       = "key",
            ["ConnectionStrings:DefaultConnection"] = "Server=mysql;Database=hrms;Uid=hrms;Pwd=secret",
            ["ALLOWED_HOSTS"]                      = allowedHosts,
            ["Hangfire:UseRedis"]                  = "true",
            ["Hangfire:RedisConnectionString"]      = "redis:6379,password=redissecret",
        };
}
