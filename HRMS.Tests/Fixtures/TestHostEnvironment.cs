using System;
using System.Security.Cryptography;

namespace HRMS.Tests.Fixtures;

/// <summary>
/// Applies the minimum host configuration that <c>Program.cs</c> reads
/// <b>while the WebApplicationBuilder is still being composed</b>
/// (JWT RS256 keys, issuer/audience, Hangfire in-memory switch).
///
/// Why environment variables and not <c>ConfigureAppConfiguration</c>:
/// with .NET minimal hosting, <c>WebApplicationFactory</c> applies
/// <c>ConfigureAppConfiguration</c> callbacks only when the host is *built*.
/// <c>Program.cs</c> calls <c>AddJwtAuthentication(builder.Configuration)</c>
/// and <c>EnvironmentValidator</c> earlier than that, so in-memory
/// configuration added by the test factory is invisible to them and the host
/// throws "Jwt:PublicKeyPem is not configured".
/// Environment variables are picked up by <c>WebApplication.CreateBuilder</c>'s
/// default environment-variable provider, so they are visible immediately.
///
/// Production behaviour is unchanged — this only affects the test process.
/// </summary>
public static class TestHostEnvironment
{
    private static readonly object _gate = new();
    private static bool _applied;

    /// <summary>Test RSA key pair shared by the integration-test hosts.</summary>
    public static (string Priv, string Pub) Keys { get; private set; }

    /// <summary>
    /// Idempotently exports the required <c>Jwt__*</c> / <c>Hangfire__*</c>
    /// environment variables for the current test process.
    /// </summary>
    public static (string Priv, string Pub) Apply()
    {
        lock (_gate)
        {
            if (_applied) return Keys;

            var (priv, pub) = TestHelpers.GenerateTestRsaKeyPair();
            Keys = (priv, pub);

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("Jwt__PrivateKeyPem",    priv);
            Environment.SetEnvironmentVariable("Jwt__PublicKeyPem",     pub);
            Environment.SetEnvironmentVariable("Jwt__Issuer",           "hrms-test");
            Environment.SetEnvironmentVariable("Jwt__Audience",         "hrms-test");
            Environment.SetEnvironmentVariable(
                "Security__EncryptionKey",
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            // Program.cs registers the MySQL health check while the host is
            // being composed, before WebApplicationFactory can replace it.
            // Use a harmless test-only connection string so registration does
            // not fail; individual integration tests replace the DbContext and
            // health check with in-memory implementations.
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                "Server=localhost;Port=3306;Database=hrms_test;User ID=test;Password=test;");
            // Signals AddHangfireJobs() to use in-memory storage instead of MySQL.
            Environment.SetEnvironmentVariable("Hangfire__UseInMemory", "true");

            _applied = true;
            return Keys;
        }
    }
}
