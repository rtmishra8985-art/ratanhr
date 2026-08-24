using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.JWT;
using HRMS.Tests.Fixtures;
using HRMS.Tests.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Extended JWT tests covering claims content, expiry, employee-id embedding,
/// and cross-key rejection.
/// </summary>
public class JwtTokenClaimsTests
{
    private static IConfiguration BuildConfig(double expiresInHours = 1) =>
        TestConfigurationFixture.Build(new Dictionary<string, string?>
        {
            ["Jwt:Audience"]         = "HRMS.API",
            ["Jwt:ExpiresInMinutes"] = ((int)(expiresInHours * 60)).ToString()
        });

    private static User MakeUser(string role = "employee") => new()
    {
        Id           = 99,
        Email        = "claims@test.com",
        Role         = role,
        FullName     = "Test User",
        CompanyId    = 7,
        PasswordHash = "x"
    };

    // ── Claims content ───────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_DoesNotContainPersonalIdentityClaims()
    {
        var svc   = new JwtService(BuildConfig(), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser());
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Email);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Name);
    }

    [Fact]
    public void GenerateToken_ContainsCorrectRoleClaim()
    {
        var svc   = new JwtService(BuildConfig(), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser("admin"));
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");
    }

    [Fact]
    public void GenerateToken_WithEmployeeId_EmbedsClaim()
    {
        var svc   = new JwtService(BuildConfig(), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser(), employeeId: "EMP0099");
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "employeeId" && c.Value == "EMP0099");
    }

    [Fact]
    public void GenerateToken_WithoutEmployeeId_DoesNotEmbedEmployeeClaim()
    {
        var svc   = new JwtService(BuildConfig(), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser());
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "employeeId");
    }

    [Fact]
    public void GenerateToken_ContainsCompanyIdClaim()
    {
        var svc   = new JwtService(BuildConfig(), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser());
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "companyId" && c.Value == "7");
    }

    // ── Expiry ───────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ExpiresInConfiguredHours()
    {
        var svc   = new JwtService(BuildConfig(expiresInHours: 2), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser());
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expectedExpiry = DateTime.UtcNow.AddHours(2);
        // Allow 30-second skew for test execution time.
        Assert.True(Math.Abs((jwt.ValidTo - expectedExpiry).TotalSeconds) < 30);
    }

    // ── Cross-key rejection ──────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_SignedWithDifferentKey_ReturnsNull()
    {
        var svcA = new JwtService(BuildConfig(), new MockLogger<JwtService>());

        // Generate a completely different RSA key pair so svcB cannot validate svcA tokens
        using var rsaB = RSA.Create(2048);
        var configB = TestConfigurationFixture.Build(new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyPem"] = rsaB.ExportRSAPrivateKeyPem(),
            ["Jwt:PublicKeyPem"]  = rsaB.ExportRSAPublicKeyPem(),
            ["Jwt:Audience"]      = "HRMS.API"
        });
        var svcB = new JwtService(configB, new MockLogger<JwtService>());

        var token = svcA.GenerateToken(MakeUser());
        Assert.Null(svcB.ValidateToken(token));
    }

    // ── Tampered token ───────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_TamperedPayload_ReturnsNull()
    {
        var svc   = new JwtService(BuildConfig(), new MockLogger<JwtService>());
        var token = svc.GenerateToken(MakeUser());

        // Flip a character in the payload segment.
        var parts = token.Split('.');
        parts[1] = new string(parts[1].Reverse().ToArray());
        var tampered = string.Join('.', parts);

        Assert.Null(svc.ValidateToken(tampered));
    }
}
