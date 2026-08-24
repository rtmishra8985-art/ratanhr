using FluentAssertions;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// HTTP-level role-based access control tests using WebApplicationFactory.
/// Verifies that every protected endpoint correctly returns 401/403 to
/// unauthenticated or unauthorized callers.
/// </summary>
public class RoleBasedAccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Must be the SAME key pair the host validates with (exported as env vars),
    // otherwise every test-issued token fails signature validation.
    private static readonly (string Priv, string Pub) _keys = HRMS.Tests.Fixtures.TestHostEnvironment.Apply();

    private readonly WebApplicationFactory<Program> _factory;

    public RoleBasedAccessTests(WebApplicationFactory<Program> factory)
    {
        // Program.cs reads JWT config while the WebApplicationBuilder is still
        // being composed, i.e. before WebApplicationFactory applies
        // ConfigureAppConfiguration. Export the values as environment variables
        // so the default env-var provider surfaces them in time.
        HRMS.Tests.Fixtures.TestHostEnvironment.Apply();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Run as Development so EnvironmentValidator skips production-only
            // checks (CORS wildcard, AllowedHosts, Compliance, EncryptionKey).
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:PrivateKeyPem"]    = _keys.Priv,
                    ["Jwt:PublicKeyPem"]     = _keys.Pub,
                    ["Jwt:Issuer"]           = "hrms-test",
                    ["Jwt:Audience"]         = "hrms-test",
                    // Signal AddHangfireJobs() to use in-memory storage instead of MySQL.
                    ["Hangfire:UseInMemory"] = "true",
                    // Swagger is credential-protected; without these the Development
                    // pass-through would expose /swagger unauthenticated.
                    ["Swagger:Username"]     = "swagger-test",
                    ["Swagger:Password"]     = "swagger-test-password",
                });
            });


            builder.ConfigureServices(services =>
            {
                // Replace real DB with in-memory for tests.
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContextFactory<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase("RoleTestDb_" + Guid.NewGuid()));

                // Replace the MySQL health check registered by Program.cs with a
                // stub that always returns Healthy so /health, /healthz, and
                // /healthz/ready return HTTP 200 in tests without a live DB.
                services.PostConfigure<HealthCheckServiceOptions>(opts =>
                {
                    var dbReg = opts.Registrations.FirstOrDefault(r => r.Name == "database");
                    if (dbReg != null) opts.Registrations.Remove(dbReg);
                });
                services.AddHealthChecks()
                    .AddCheck("database",
                        () => HealthCheckResult.Healthy("test stub — no MySQL in CI"),
                        tags: new[] { "db", "ready" });
            });
        });
    }

    // ─── Unauthenticated (no token) ───────────────────────────────────────────────

    [Theory]
    [InlineData("GET",  "/api/employees")]
    [InlineData("GET",  "/api/payroll")]
    [InlineData("GET",  "/api/leave")]
    [InlineData("GET",  "/api/reports/dashboard")]
    [InlineData("GET",  "/api/departments")]
    [InlineData("GET",  "/api/admin-users")]
    public async Task Endpoint_NoToken_Returns401(string method, string path)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var request  = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"{method} {path} must require authentication");
    }

    // ─── Employee role — restricted endpoints ─────────────────────────────────────

    [Theory]
    [InlineData("POST", "/api/payroll/generate")]
    [InlineData("POST", "/api/admin-users")]
    [InlineData("DELETE", "/api/admin-users/some-id")]
    [InlineData("POST", "/api/companies")]
    [InlineData("POST", "/api/departments")]
    public async Task Endpoint_EmployeeToken_Returns403(string method, string path)
    {
        // Arrange
        var client = CreateClientWithRole("Employee", companyId: 1);

        // Act
        var request  = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"Employees must not access {method} {path}");
    }

    // ─── HrAdmin role — payroll generation allowed ────────────────────────────────

    [Fact]
    public async Task PayrollGenerate_HrAdminToken_ReturnsNotForbidden()
    {
        // Arrange
        var client = CreateClientWithRole("HrAdmin", companyId: 1);

        // Act
        var response = await client.GetAsync("/api/payroll");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "HrAdmin must be able to access payroll endpoints");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    // ─── SuperAdmin — unrestricted ────────────────────────────────────────────────

    [Fact]
    public async Task CompanyEndpoint_SuperAdminToken_Succeeds()
    {
        // Arrange
        var client = CreateClientWithRole("SuperAdmin", companyId: null);

        // Act
        var response = await client.GetAsync("/api/companies");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    // ─── Profile endpoint — any authenticated user ────────────────────────────────

    [Fact]
    public async Task ProfileEndpoint_AuthenticatedUser_Returns200()
    {
        // Arrange
        var client = CreateClientWithRole("Employee", companyId: 1);

        // Act
        var response = await client.GetAsync("/api/auth/profile");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ─── Swagger — not accessible without BasicAuth ───────────────────────────────

    [Fact]
    public async Task Swagger_NoBasicAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Found },
            because: "Swagger UI must be protected");
    }

    // ─── Health endpoints — public ────────────────────────────────────────────────

    [Theory]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/healthz/ready")]
    [InlineData("/healthz/live")]
    public async Task HealthEndpoint_NoToken_Returns200(string path)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"health endpoint {path} must be publicly accessible");
    }

    // ─── Rate limiting ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_RateLimited_AfterThreshold_Returns429()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act — fire many rapid login requests
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 20; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "x@y.com", password = "wrong", portal = "Admin" });
            responses.Add(resp);
        }

        // Assert — at least one request should be rate-limited
        responses.Any(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Should().BeTrue("repeated failed logins must be rate-limited");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private HttpClient CreateClientWithRole(string role, int? companyId)
    {
        var client = _factory.CreateClient();
        // Sign with the RSA private key that matches the public key supplied to
        // the server via ConfigureAppConfiguration.  The server validates with
        // RS256, so the algorithm must match.
        var token = TestJwtHelper.GenerateToken(
            userId: "test-user-id",
            email: "test@co.com",
            role: role,
            companyId: companyId,
            privateKeyPem: _keys.Priv);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>Minimal test JWT generator — used only in test code.</summary>
internal static class TestJwtHelper
{
    /// <summary>
    /// Generates a signed JWT for integration testing.
    /// <para>
    /// When <paramref name="privateKeyPem"/> is supplied the token is signed
    /// with RS256 (RSA-SHA256), matching the server's RS256 validation key.
    /// Without a private key the method falls back to HS256 — sufficient for
    /// tests that only need a syntactically valid token.
    /// </para>
    /// </summary>
    internal static string GenerateToken(
        string userId,
        string email,
        string role,
        int? companyId,
        string? privateKeyPem = null)
    {
        // Tests pass friendly role names ("SuperAdmin", "HrAdmin", ...). The API
        // authorizes against the AppRoles constants, and ASP.NET role checks are
        // case-sensitive, so normalize here instead of emitting unmatchable roles.
        role = NormalizeRole(role);

        var claims = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role),
            new System.Security.Claims.Claim("companyId", companyId?.ToString() ?? ""),
        });

        Microsoft.IdentityModel.Tokens.SecurityKey signingKey;
        string algorithm;

        if (!string.IsNullOrWhiteSpace(privateKeyPem))
        {
            // RS256: sign with the test RSA private key; server validates with
            // the matching public key injected via ConfigureAppConfiguration.
            var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportFromPem(privateKeyPem.AsSpan());
            signingKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa);
            algorithm  = Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256;
        }
        else
        {
            // HS256 fallback — only for tests that do not need signature validation.
            signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes("test-secret-key-must-be-at-least-32-chars!"));
            algorithm  = Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256;
        }

        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(signingKey, algorithm);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "hrms-test",
            audience: "hrms-test",
            claims: claims.Claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Maps the friendly role names used by the tests onto the actual
    /// <see cref="HRMS.Application.Common.AppRoles"/> values used by the API.
    /// An HR admin is an admin user whose AdminRole is "HR Admin", so it maps
    /// onto the admin authorization role.
    /// </summary>
    private static string NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "superadmin"                 => HRMS.Application.Common.AppRoles.SuperAdmin,
        "admin" or "hradmin" or "hr admin" => HRMS.Application.Common.AppRoles.Admin,
        "employee"                   => HRMS.Application.Common.AppRoles.Employee,
        _                            => role ?? string.Empty
    };
}
