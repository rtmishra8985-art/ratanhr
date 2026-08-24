// Updated: migrated from HS256 (Jwt:Key) to RS256 (Jwt:PrivateKeyPem / Jwt:PublicKeyPem).
// JwtService now requires an RSA key pair; symmetric Jwt:Key no longer exists.
// Tests generate a fresh RSA-2048 key pair at class initialisation time (once per test
// run) to avoid the ~100 ms RSA cost on every test method.
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.JWT;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HRMS.Tests;

public class JwtServiceTests
{
    // ── Key pair generated once per test-class instance ───────────────────
    private static readonly (string Priv, string Pub) Keys =
        TestHelpers.GenerateTestRsaKeyPair();

    private readonly Mock<ILogger<JwtService>> _jwtLogger = new();

    private static IConfiguration BuildConfig(string? privatePem = null, string? publicPem = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyPem"]    = privatePem ?? Keys.Priv,
                ["Jwt:PublicKeyPem"]     = publicPem  ?? Keys.Pub,
                ["Jwt:Issuer"]           = "HRMS.API",
                ["Jwt:Audience"]         = "HRMS.API",
                ["Jwt:ExpiresInMinutes"] = "30"
            }).Build();

    // ── Happy-path token round-trip ────────────────────────────────────────

    [Fact]
    public void GenerateToken_ThenValidateToken_ReturnsSameUserId()
    {
        var service = new JwtService(BuildConfig(), _jwtLogger.Object);
        var user = new User { Id = 42, Email = "jwt@test.com", Role = "employee", PasswordHash = "x" };

        var token  = service.GenerateToken(user, employeeId: "EMP0042");
        var userId = service.ValidateToken(token);

        Assert.Equal(42, userId);
    }

    [Fact]
    public void GenerateToken_WithoutEmployeeId_StillValidates()
    {
        var service = new JwtService(BuildConfig(), _jwtLogger.Object);
        var user = new User { Id = 7, Email = "admin@test.com", Role = "admin", PasswordHash = "x" };

        var token  = service.GenerateToken(user);
        var userId = service.ValidateToken(token);

        Assert.Equal(7, userId);
    }

    [Fact]
    public void GenerateToken_WithEscapedNewlinePem_ThenValidateToken_ReturnsSameUserId()
    {
        // Docker Compose dotenv values commonly encode PEM line breaks as literal "\\n".
        var escapedPrivate = Keys.Priv.Replace("\r\n", "\n").Replace("\n", "\\n");
        var escapedPublic  = Keys.Pub.Replace("\r\n", "\n").Replace("\n", "\\n");
        var service = new JwtService(BuildConfig(escapedPrivate, escapedPublic), _jwtLogger.Object);
        var user = new User { Id = 73, Email = "escaped-pem@test.com", Role = "employee", PasswordHash = "x" };

        var token = service.GenerateToken(user);

        Assert.Equal(73, service.ValidateToken(token));
    }

    // ── Rejection cases ────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_WithGarbage_ReturnsNull()
    {
        var service = new JwtService(BuildConfig(), _jwtLogger.Object);
        Assert.Null(service.ValidateToken("not-a-real-token"));
    }

    [Fact]
    public void ValidateToken_EmptyString_ReturnsNull()
    {
        var service = new JwtService(BuildConfig(), _jwtLogger.Object);
        Assert.Null(service.ValidateToken(string.Empty));
    }

    [Fact]
    public void ValidateToken_TokenSignedWithDifferentPrivateKey_ReturnsNull()
    {
        // Tokens signed with key-A must be rejected when validated against key-B.
        // This verifies that JwtService correctly rejects cross-key tokens
        // (e.g. a forged token or a token from a previous key rotation).
        var (priv2, pub2) = TestHelpers.GenerateTestRsaKeyPair();

        var signer    = new JwtService(BuildConfig(), _jwtLogger.Object);           // signed with Keys.Priv
        var validator = new JwtService(BuildConfig(Keys.Priv, pub2), _jwtLogger.Object); // validates with pub2

        var user  = new User { Id = 99, Email = "hack@test.com", Role = "employee", PasswordHash = "x" };
        var token = signer.GenerateToken(user);

        // Token signed by Keys.Priv but validated against pub2 (different key) → null
        Assert.Null(validator.ValidateToken(token));
    }

    // ── Key loading failures ───────────────────────────────────────────────

    [Fact]
    public void GenerateToken_PrivateKeyMissing_ThrowsInvalidOperationException()
    {
        var configNoKey = BuildConfig(privatePem: string.Empty);
        var service = new JwtService(configNoKey, _jwtLogger.Object);
        var user    = new User { Id = 1, Email = "x@test.com", Role = "employee", PasswordHash = "x" };

        // Key is loaded lazily on first use; exception surfaces here.
        Assert.Throws<InvalidOperationException>(() => service.GenerateToken(user));
    }

    [Fact]
    public void ValidateToken_PublicKeyMissing_ReturnsNull()
    {
        // With no public key configured, ValidateToken must catch the exception and
        // return null rather than propagating the InvalidOperationException to callers.
        var configNoKey = BuildConfig(publicPem: string.Empty);
        var service = new JwtService(configNoKey, _jwtLogger.Object);

        // Generate a valid token with a properly-configured service first.
        var goodService = new JwtService(BuildConfig(), _jwtLogger.Object);
        var user  = new User { Id = 2, Email = "y@test.com", Role = "employee", PasswordHash = "x" };
        var token = goodService.GenerateToken(user);

        // Validation against a service with no public key must not throw.
        // It may throw InvalidOperationException internally, but ValidateToken
        // wraps everything in try/catch and returns null on any error.
        var result = service.ValidateToken(token);
        Assert.Null(result);
    }
}
