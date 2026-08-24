// H-04 FIX: Controller-level IDOR integration tests using WebApplicationFactory.
//
// Previous IDOR tests only called repository methods directly (no HTTP stack, no
// middleware, no routing). These tests spin up the full ASP.NET Core pipeline via
// WebApplicationFactory so we can prove that the H-01 fix prevents cross-tenant
// profile access end-to-end: authentication middleware, route handler, controller,
// service, and EF Core global query filters all run as they do in production.
//
// External dependencies (PostgreSQL, Redis, Hangfire, ClamAV, SMTP) are replaced with
// in-memory or null stubs so the tests run in any CI environment without live infra.
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using HRMS.Application.DTOs.Employee;
using HRMS.Tests; // TestHelpers.GenerateTestRsaKeyPair()
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace HRMS.Tests.Security;

// ── Custom factory — stubs external dependencies ───────────────────────────
/// <summary>
/// Replaces production infrastructure with lightweight in-memory stubs so the
/// full HTTP pipeline can run in a test process without live services.
/// </summary>
public sealed class HrmsTestWebAppFactory : WebApplicationFactory<Program>
{
    // The DbContextOptions action runs once per scope, so a Guid computed inside it
    // would give every request its own empty in-memory store. Compute the name once
    // per factory instance instead.
    private readonly string _dbName = "hrms_idor_test_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs consumes JWT config while the WebApplicationBuilder is being
        // composed — earlier than ConfigureAppConfiguration is applied by
        // WebApplicationFactory. Export env vars so they are visible in time.
        var (priv, pub) = HRMS.Tests.Fixtures.TestHostEnvironment.Apply();

        // Development: EnvironmentValidator skips production-only checks
        // (CORS wildcard, AllowedHosts, Compliance, EncryptionKey).
        builder.UseEnvironment("Development");

        // Provide the minimum configuration EnvironmentValidator and
        // AddJwtAuthentication() require.  JWT keys are generated fresh for
        // the test process so no secrets live in source control.
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyPem"]    = priv,
                ["Jwt:PublicKeyPem"]     = pub,
                ["Jwt:Issuer"]           = "hrms-test",
                ["Jwt:Audience"]         = "hrms-test",
                // Signal AddHangfireJobs() to use in-memory storage instead of MySQL.
                ["Hangfire:UseInMemory"] = "true",
            });
        });


        builder.ConfigureServices(services =>
        {
            // Replace EF Core → in-memory database (unique per factory instance)
            services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContextFactory<ApplicationDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));

            // Replace distributed cache (Redis) with null implementation
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            // Disable Hangfire / background services — not needed for IDOR tests
            services.RemoveAll<Microsoft.AspNetCore.Hosting.IStartupFilter>();
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace the JWT authentication scheme with a test scheme that accepts
            // a special "Test-Auth" header carrying a principal built from JSON claims.
            services.Configure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = "Test";
                opts.DefaultChallengeScheme    = "Test";
            });
            services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}

/// <summary>
/// Reads a JSON claim set from the "X-Test-Claims" request header and returns
/// the corresponding ClaimsPrincipal as the authenticated user.
/// Header value: base64(json([{"type":"...","value":"..."}, ...]))
/// </summary>
internal sealed class TestAuthHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers["X-Test-Claims"].FirstOrDefault();
        if (string.IsNullOrEmpty(header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var json    = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header));
        // The header payload uses camelCase keys ("type"/"value") while ClaimDto is a
        // PascalCase record, so deserialization must be case-insensitive; otherwise every
        // property binds to null and new Claim(null, null) throws ArgumentNullException.
        var dtos    = JsonSerializer.Deserialize<List<ClaimDto>>(
                          json,
                          new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        var claims  = dtos.Select(d => new Claim(d.Type, d.Value)).ToList();
        var identity = new ClaimsIdentity(claims, "Test");
        var ticket  = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private sealed record ClaimDto(string Type, string Value);
}

// ── Helpers ────────────────────────────────────────────────────────────────
file static class TestClaimsHelper
{
    public static string BuildHeader(string role, int companyId, int userId = 1, string? employeeId = null)
    {
        var claims = new List<object>
        {
            new { type = ClaimTypes.NameIdentifier, value = userId.ToString() },
            new { type = ClaimTypes.Role,            value = role              },
            new { type = "companyId",                value = companyId.ToString() },
        };
        if (employeeId != null)
            claims.Add(new { type = "employeeId", value = employeeId });

        var json    = JsonSerializer.Serialize(claims);
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        return encoded;
    }

    public static HttpRequestMessage WithClaims(this HttpRequestMessage req, string role,
        int companyId, int userId = 1, string? employeeId = null)
    {
        req.Headers.Add("X-Test-Claims", BuildHeader(role, companyId, userId, employeeId));
        return req;
    }
}

// ── H-04 controller-level IDOR integration tests ───────────────────────────
/// <summary>
/// Verifies that the full HTTP pipeline (middleware → routing → controller → service →
/// EF Core) enforces tenant isolation on GET /api/my/profile.
/// </summary>
public class EmployeeSelfControllerIdorIntegrationTests
    : IClassFixture<HrmsTestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly HrmsTestWebAppFactory _factory;

    public EmployeeSelfControllerIdorIntegrationTests(HrmsTestWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // Seed two employees in different companies and return a client scoped to
    // the factory's in-memory database.
    private async Task SeedEmployees(ApplicationDbContext db)
    {
        db.Employees.AddRange(
            new HRMS.Domain.Entities.Employee.Employee
            {
                EmployeeCode = "EMP-COMPANY1",   // FIX 8: EmployeeId is [NotMapped] int; EmployeeCode is the string business key
                FullName     = "Alice Smith",
                CompanyId    = 1,
                Email        = "alice@c1.example",
                IsActive     = true
            },
            new HRMS.Domain.Entities.Employee.Employee
            {
                EmployeeCode = "EMP-COMPANY2",   // FIX 8: same fix
                FullName     = "Bob Jones",
                CompanyId    = 2,
                Email        = "bob@c2.example",
                IsActive     = true
            }
        );
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMyProfile_CrossTenant_Manipulated_EmployeeId_Returns404()
    {
        // Arrange: seed employees and scope the request as Company-1 employee
        // who manipulates their employeeId claim to point to Company-2's employee.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedEmployees(db);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/my/profile")
            .WithClaims(role: "employee", companyId: 1, employeeId: "EMP-COMPANY2"); // IDOR attempt

        // Act
        var response = await _client.SendAsync(req);

        // Assert: H-01 fix must return 404 (not 200 with another tenant's data)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_SameTenant_ValidEmployeeId_Returns200()
    {
        // Arrange: legitimate access — Company-1 employee fetches their own profile
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedEmployees(db);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/my/profile")
            .WithClaims(role: "employee", companyId: 1, employeeId: "EMP-COMPANY1"); // own record

        // Act
        var response = await _client.SendAsync(req);

        // Assert: legitimate request must succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_UnauthenticatedRequest_Returns401()
    {
        // Arrange: no X-Test-Claims header → anonymous
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/my/profile");

        // Act
        var response = await _client.SendAsync(req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
