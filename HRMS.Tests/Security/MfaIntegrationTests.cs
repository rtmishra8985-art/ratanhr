// MFA Integration Tests — replaces MfaBypassSkeletonTests.cs placeholder.
//
// Three test suites covering the full MFA threat model:
//
//   A. Happy path  — login → MFA verify → protected access (service-level)
//   B. Bypass path — temp token must NOT grant access to protected resources (HTTP-level)
//   C. Refresh token — pre-MFA refresh tokens must be invalidated (service-level)
//
// Tests A and C run at the service layer with real JwtService (RS256) + real MfaService
// (TOTP via OtpNet) + ApplicationDbContext (InMemory). No mocks of the JWT library.
//
// Test B adds a real TestServer with the JWT Bearer middleware + tenant isolation
// middleware, demonstrating the HTTP-level enforcement without needing MySQL or Redis.

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.JWT;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using Xunit;

namespace HRMS.Tests.Security;

// ── Shared test RSA key pair ─────────────────────────────────────────────────
// Generated once for all MFA tests; RSA keygen is ~100 ms so it must not run
// inside every [Fact]. Both JwtService instances and the TestServer use this pair.

internal static class MfaTestKeys
{
    private static readonly (string Priv, string Pub) _pair =
        TestHelpers.GenerateTestRsaKeyPair();

    public static string PrivatePem => _pair.Priv;
    public static string PublicPem  => _pair.Pub;

    // Shared RSA instances. IdentityModel caches SignatureProviders globally by key,
    // so disposing a per-test RSA breaks signature validation in subsequent tests
    // ("The signature key was not found"). These are process-wide and never disposed.
    private static readonly Lazy<RSA> _publicRsa = new(() =>
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_pair.Pub);
        return rsa;
    });

    private static readonly Lazy<RSA> _privateRsa = new(() =>
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_pair.Priv);
        return rsa;
    });

    public static RSA PublicRsa  => _publicRsa.Value;
    public static RSA PrivateRsa => _privateRsa.Value;

    public static IConfiguration BuildJwtConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyPem"]      = PrivatePem,
                ["Jwt:PublicKeyPem"]       = PublicPem,
                ["Jwt:Issuer"]             = "hrms-mfa-test",
                ["Jwt:Audience"]           = "hrms-mfa-test-client",
                ["Jwt:ExpiresInMinutes"]   = "30",
                ["Mfa:Issuer"]             = "HRMS-Tests",
            })
            .Build();

    public static JwtService NewJwtService() =>
        new(BuildJwtConfig(), NullLogger<JwtService>.Instance);
}

// ── Test infrastructure helpers ───────────────────────────────────────────────

/// <summary>Minimal IHostEnvironment implementation for AuthService constructor.</summary>
internal sealed class MfaTestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName  { get; set; } = "Testing";
    public string ApplicationName  { get; set; } = "HRMS.Tests";
    public string ContentRootPath  { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

/// <summary>Helpers shared across all MFA test classes.</summary>
internal static class MfaTestFactory
{
    public static AuthService BuildAuthService(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        JwtService jwt)
    {
        // FileStorageService is only used by UpdateProfilePictureAsync — not exercised here.
        var fsSvc = new HRMS.Infrastructure.FileStorage.FileStorageService(
            Path.GetTempPath(),
            Options.Create(new HRMS.Infrastructure.Security.FileUploadOptions()));

        return new AuthService(
            db, jwt,
            NullLogger<AuthService>.Instance,
            MfaTestKeys.BuildJwtConfig(),
            new Mocks.MockAuditService(),
            new Mocks.MockEmailService(),
            fsSvc,
            new MfaTestHostEnvironment());
    }

    public static MfaService BuildMfaService(
        HRMS.Infrastructure.Data.ApplicationDbContext db) =>
        new(db, MfaTestKeys.BuildJwtConfig(), NullLogger<MfaService>.Instance);

    public static async Task<User> SeedMfaUser(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        string totpSecret,
        string? role = null,
        int companyId = 1)
    {
        var user = new User
        {
            Email        = $"mfa-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
            Role         = role ?? AppRoles.Admin,
            CompanyId    = companyId,
            IsActive     = true,
            IsMfaEnabled = true,
            TotpSecret   = totpSecret,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// A. MFA Happy Path — service-level integration
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies the full MFA authentication flow using real services, an in-memory
/// database, and genuine RSA-256 JWT signing. No HTTP layer required.
/// </summary>
public class MfaHappyPathTests
{
    private const string KnownSecret = "JBSWY3DPEHPK3PXP";

    // A-1: Login with an MFA-enabled user → response must contain MfaRequired=true
    //      and a TempToken, NOT a full JWT or RefreshToken.
    [Fact]
    public async Task A1_LoginWithMfaUser_ReturnsMfaRequiredAndTempToken()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var user     = await MfaTestFactory.SeedMfaUser(db, KnownSecret);
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var (result, error) = await authSvc.LoginAsync(
            new LoginDto { Email = user.Email, Password = "Test@1234", Portal = AppRoles.Admin });

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.MfaRequired,
            "LoginAsync for an MFA-enabled user must return MfaRequired=true.");
        Assert.NotEmpty(result.TempToken!);
        // No full JWT / refresh token until TOTP is verified (DTO defaults to "" rather than null).
        Assert.True(string.IsNullOrEmpty(result.Token),
            "LoginAsync must not issue a full JWT before MFA verification.");
        Assert.True(string.IsNullOrEmpty(result.RefreshToken),
            "LoginAsync must not issue a refresh token before MFA verification.");
    }

    // A-2: TempToken payload must contain mfa_pending=true and must NOT contain
    //      the role or companyId claims that appear only in a full session JWT.
    [Fact]
    public async Task A2_TempToken_ContainsMfaPendingClaim_AndLacksRoleAndCompanyId()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var user     = await MfaTestFactory.SeedMfaUser(db, KnownSecret);
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var (result, _) = await authSvc.LoginAsync(
            new LoginDto { Email = user.Email, Password = "Test@1234", Portal = AppRoles.Admin });

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(result!.TempToken);

        Assert.Equal("true", parsed.Claims.FirstOrDefault(c => c.Type == "mfa_pending")?.Value);
        Assert.Null(parsed.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role));
        Assert.Null(parsed.Claims.FirstOrDefault(c => c.Type == "companyId"));
    }

    // A-3: ValidateTempToken accepts the TempToken; ValidateTempToken rejects a full JWT.
    //      This confirms the two token types are mutually exclusive.
    [Fact]
    public async Task A3_TempToken_AcceptedByTempValidator_RejectedAsFullToken()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var user     = await MfaTestFactory.SeedMfaUser(db, KnownSecret);
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var (loginResult, _) = await authSvc.LoginAsync(
            new LoginDto { Email = user.Email, Password = "Test@1234", Portal = AppRoles.Admin });

        var tempToken = loginResult!.TempToken!;

        // ValidateTempToken must succeed for a TempToken
        var tempPrincipal = jwt.ValidateTempToken(tempToken);
        Assert.NotNull(tempPrincipal);
        Assert.Equal("true", tempPrincipal!.FindFirst("mfa_pending")?.Value);

        // A full session JWT must NOT carry mfa_pending → ValidateTempToken rejects it
        var fullToken = jwt.GenerateToken(
            new User { Id = user.Id, Email = user.Email, Role = user.Role, CompanyId = user.CompanyId });
        var shouldBeNull = jwt.ValidateTempToken(fullToken);
        Assert.Null(shouldBeNull);
    }

    // A-4: Full flow: login → ValidateTempToken → VerifyMfaAsync (valid TOTP)
    //      → IssueRefreshTokenAsync → confirm MfaVerified=true in DB.
    [Fact]
    public async Task A4_FullMfaFlow_LoginVerifyTotp_ProducesAuthenticatedSession()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var user     = await MfaTestFactory.SeedMfaUser(db, KnownSecret);
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);
        var mfaSvc   = MfaTestFactory.BuildMfaService(db);

        // Step 1: Login → MFA required
        var (loginResult, error) = await authSvc.LoginAsync(
            new LoginDto { Email = user.Email, Password = "Test@1234", Portal = AppRoles.Admin });
        Assert.Null(error);
        Assert.True(loginResult!.MfaRequired);

        // Step 2: Extract userId from TempToken
        var principal = jwt.ValidateTempToken(loginResult.TempToken!);
        Assert.NotNull(principal);
        var userId = int.Parse(principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Assert.Equal(user.Id, userId);

        // Step 3: Verify TOTP — compute real code from the known secret
        var code = new Totp(Base32Encoding.ToBytes(KnownSecret)).ComputeTotp();
        var ok   = await mfaSvc.VerifyMfaAsync(userId, code);
        Assert.True(ok, "Valid TOTP code must be accepted by MfaService.VerifyMfaAsync.");

        // Step 4: Issue refresh token (MfaVerified=true)
        var refreshRaw = await authSvc.IssueRefreshTokenAsync(userId);
        Assert.NotEmpty(refreshRaw);

        // Step 5: Confirm the stored refresh token carries MfaVerified=true
        var hash = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshRaw)));
        var rt = db.RefreshTokens.FirstOrDefault(r => r.TokenHash == hash);
        Assert.NotNull(rt);
        Assert.True(rt!.MfaVerified,
            "Refresh token issued after TOTP verification must have MfaVerified=true.");

        // Step 6: The same refresh token must successfully renew a session
        var refreshResult = await authSvc.RefreshTokenAsync(refreshRaw);
        Assert.NotNull(refreshResult);
        Assert.NotEmpty(refreshResult!.Token!);
    }

    // A-5: A valid TOTP code is accepted; an invalid code is rejected.
    [Fact]
    public async Task A5_VerifyMfa_ValidCode_Accepted_InvalidCode_Rejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user     = await MfaTestFactory.SeedMfaUser(db, KnownSecret);
        var mfaSvc   = MfaTestFactory.BuildMfaService(db);

        var validCode = new Totp(Base32Encoding.ToBytes(KnownSecret)).ComputeTotp();
        Assert.True(await mfaSvc.VerifyMfaAsync(user.Id, validCode));
        Assert.False(await mfaSvc.VerifyMfaAsync(user.Id, "000000"),
            "An obviously invalid TOTP code must be rejected.");
    }

    // A-6: Confirm only a temporary token is returned (no refresh token).
    [Fact]
    public async Task A6_LoginWithMfaUser_DoesNotIssueRefreshToken()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var user     = await MfaTestFactory.SeedMfaUser(db, KnownSecret);
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var (result, _) = await authSvc.LoginAsync(
            new LoginDto { Email = user.Email, Password = "Test@1234", Portal = AppRoles.Admin });

        Assert.True(result!.MfaRequired);
        // No refresh token must be stored for this login step
        var anyRefreshTokens = db.RefreshTokens.Where(r => r.UserId == user.Id).ToList();
        Assert.Empty(anyRefreshTokens);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// B. MFA Bypass Path — HTTP layer
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies at the HTTP transport layer that a TempToken cannot be used to access
/// a protected API endpoint. Uses a real <see cref="TestServer"/> with the
/// production JWT bearer middleware and tenant isolation middleware.
/// No MySQL, Redis, or Hangfire required.
/// </summary>
public sealed class MfaBypassHttpTests : IDisposable
{
    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly JwtService  _jwt;

    // RSA key for JWT bearer validation (same key pair as JwtService).
    // Must be kept alive for the lifetime of _server because JwtBearer holds a reference.
    private readonly RSA _rsaForValidation;

    public MfaBypassHttpTests()
    {
        _jwt = MfaTestKeys.NewJwtService();
        var jwtConfig = MfaTestKeys.BuildJwtConfig();

        // Load the RSA public key for the JWT bearer middleware
        _rsaForValidation = MfaTestKeys.PublicRsa;
        var validationKey = new RsaSecurityKey(_rsaForValidation);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey        = validationKey,
                    ValidAlgorithms         = new[] { SecurityAlgorithms.RsaSha256 },
                    ValidateIssuer          = true,
                    ValidIssuer             = jwtConfig["Jwt:Issuer"],
                    ValidateAudience        = true,
                    ValidAudience           = jwtConfig["Jwt:Audience"],
                    ValidateLifetime        = true,
                };
            });

        builder.Services.AddAuthorization(opt =>
        {
            opt.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        // ── Tenant isolation middleware (mirrors production Program.cs) ──────
        // Any authenticated non-superadmin request without a valid companyId claim
        // returns HTTP 403 Forbidden.  A TempToken carries only sub + mfa_pending,
        // so it is blocked here even if the JWT bearer middleware accepted it.
        app.Use(async (ctx, next) =>
        {
            if (ctx.User.Identity?.IsAuthenticated == true)
            {
                var isSuperAdmin = ctx.User.IsInRole(AppRoles.SuperAdmin);
                if (!isSuperAdmin)
                {
                    if (!int.TryParse(ctx.User.FindFirst("companyId")?.Value,
                            out var cid) || cid <= 0)
                    {
                        ctx.Response.StatusCode  = 403;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync(
                            """{"success":false,"message":"A valid company scope is required."}""");
                        return;
                    }
                }
            }
            await next();
        });

        // A protected endpoint (the fallback policy requires authentication).
        app.MapGet("/test/protected", [Authorize] () => Results.Ok(new { ok = true }));

        app.Start();
        _server = app.GetTestServer();
        _client = _server.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        // _rsaForValidation is process-wide shared; do NOT dispose it here.
    }

    // B-1: Use only the TempToken (no MFA verify step) → must NOT receive HTTP 200.
    [Fact]
    public async Task B1_TempToken_AsBearer_ProtectedEndpointIsNotHttp200()
    {
        var tempToken = _jwt.GenerateTempToken(userId: 42);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tempToken);

        var response = await _client.GetAsync("/test/protected");

        // The temp token must not grant access.
        // 401 → rejected by JWT bearer middleware (unlikely given same key/issuer/audience).
        // 403 → accepted by bearer, blocked by tenant middleware (missing companyId claim).
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403 for a TempToken; got {(int)response.StatusCode} {response.StatusCode}.");
    }

    // B-2: A full session JWT (with companyId claim) → protected endpoint returns 200.
    [Fact]
    public async Task B2_FullJwt_WithCompanyId_ProtectedEndpointReturns200()
    {
        var fullToken = _jwt.GenerateToken(new User
        {
            Id        = 1,
            Email     = "admin@test.com",
            Role      = AppRoles.Admin,
            CompanyId = 1,
        });
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", fullToken);

        var response = await _client.GetAsync("/test/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // B-3: No Authorization header → 401 Unauthorized.
    [Fact]
    public async Task B3_NoToken_ProtectedEndpointReturns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/test/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // B-4: Expired token → 401 Unauthorized.
    [Fact]
    public async Task B4_ExpiredToken_ProtectedEndpointReturns401()
    {
        // Issue a full token then build an identical one that has already expired.
        var jwtConfig = MfaTestKeys.BuildJwtConfig();
        var signingKey = new RsaSecurityKey(MfaTestKeys.PrivateRsa);
        var creds      = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var expiredToken = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer:    jwtConfig["Jwt:Issuer"],
                audience:  jwtConfig["Jwt:Audience"],
                claims:    new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, AppRoles.Admin),
                    new Claim("companyId", "1"),
                },
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires:   DateTime.UtcNow.AddHours(-1), // already expired
                signingCredentials: creds));

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await _client.GetAsync("/test/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C. Refresh Token Path — service-level integration
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies refresh token behaviour around MFA enforcement:
///   C-1: Pre-MFA refresh token (MfaVerified=false) cannot create a full session
///        for an MFA-enabled user.
///   C-2: Post-TOTP refresh token (MfaVerified=true) successfully renews a session.
///   C-3: Token rotation — a refresh token is revoked after use.
///   C-4: Explicit logout revokes the token, preventing any further reuse.
/// </summary>
public class MfaRefreshTokenTests
{
    private static async Task<(User user, string refreshRaw)> SeedPreMfaRefreshToken(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        bool enableMfaOnUser)
    {
        var user = new User
        {
            Email        = $"refresh-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
            Role         = AppRoles.Admin,
            CompanyId    = 1,
            IsActive     = true,
            IsMfaEnabled = false, // starts disabled; enabled after token is seeded
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Manually insert a pre-MFA refresh token (MfaVerified=false).
        var raw  = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId      = user.Id,
            TokenHash   = hash,
            ExpiresAt   = DateTime.UtcNow.AddDays(7),
            MfaVerified = false,
        });
        await db.SaveChangesAsync();

        if (enableMfaOnUser)
        {
            user.IsMfaEnabled = true;
            await db.SaveChangesAsync();
        }
        return (user, raw);
    }

    // C-1: Pre-MFA refresh token → null result when MFA is now enabled on the account.
    [Fact]
    public async Task C1_PreMfaRefreshToken_CannotCreateSession_AfterMfaEnabled()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var (_, refreshRaw) = await SeedPreMfaRefreshToken(db, enableMfaOnUser: true);

        var result = await authSvc.RefreshTokenAsync(refreshRaw);

        Assert.Null(result);
    }

    // C-2: Post-TOTP refresh token (MfaVerified=true) successfully creates a session.
    [Fact]
    public async Task C2_PostMfaRefreshToken_CreatesSession()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var user = new User
        {
            Email        = $"c2-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
            Role         = AppRoles.Admin,
            CompanyId    = 1,
            IsActive     = true,
            IsMfaEnabled = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // IssueRefreshTokenAsync always sets MfaVerified=true
        var refreshRaw = await authSvc.IssueRefreshTokenAsync(user.Id);

        var result = await authSvc.RefreshTokenAsync(refreshRaw);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Token!);
        Assert.NotEmpty(result.RefreshToken!);
    }

    // C-3: Refresh tokens are rotated — after one use the original token is revoked.
    [Fact]
    public async Task C3_RefreshToken_IsRotated_OriginalCannotBeReused()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var user = new User
        {
            Email        = $"c3-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
            Role         = AppRoles.Admin,
            CompanyId    = 1,
            IsActive     = true,
            IsMfaEnabled = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var refreshRaw = await authSvc.IssueRefreshTokenAsync(user.Id);

        // First use — succeeds and rotates the token
        var first = await authSvc.RefreshTokenAsync(refreshRaw);
        Assert.NotNull(first);

        // Second use of the same (now-revoked) token must fail
        var second = await authSvc.RefreshTokenAsync(refreshRaw);
        Assert.Null(second);
    }

    // C-4: Explicit logout invalidates the refresh token; subsequent use returns null.
    [Fact]
    public async Task C4_ExplicitLogout_RevokesRefreshToken()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var jwt      = MfaTestKeys.NewJwtService();
        var authSvc  = MfaTestFactory.BuildAuthService(db, jwt);

        var user = new User
        {
            Email        = $"c4-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@1234"),
            Role         = AppRoles.Admin,
            CompanyId    = 1,
            IsActive     = true,
            IsMfaEnabled = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var refreshRaw = await authSvc.IssueRefreshTokenAsync(user.Id);

        // Revoke via logout
        var revoked = await authSvc.LogoutAsync(refreshRaw);
        Assert.True(revoked);

        // Revoked token must not produce a session
        var result = await authSvc.RefreshTokenAsync(refreshRaw);
        Assert.Null(result);
    }
}
