using System.Net;
using HRMS.Tests.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// Runtime verification of the actual endpoint routing table. This deliberately
/// does not inspect controller source or use a hard-coded controller inventory.
/// </summary>
public sealed class AuthorizationEndpointRuntimeAuditTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationEndpointRuntimeAuditTests(WebApplicationFactory<Program> factory)
    {
        var keys = TestHostEnvironment.Apply();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:PrivateKeyPem"] = keys.Priv,
                    ["Jwt:PublicKeyPem"] = keys.Pub,
                    ["Jwt:Issuer"] = "hrms-test",
                    ["Jwt:Audience"] = "hrms-test",
                    ["Hangfire:UseInMemory"] = "true",
                    ["Swagger:Username"] = "swagger-test",
                    ["Swagger:Password"] = "swagger-test-password",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<HRMS.Infrastructure.Data.ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<HRMS.Infrastructure.Data.ApplicationDbContext>>();
                services.RemoveAll<HRMS.Infrastructure.Data.ApplicationDbContext>();
                services.AddDbContextFactory<HRMS.Infrastructure.Data.ApplicationDbContext>(
                    options => options.UseInMemoryDatabase("EndpointAudit_" + Guid.NewGuid()));
            });
        });
    }

    [Fact]
    public async Task RuntimeEndpointMetadata_UsesExactAnonymousAllowList_AndRateLimits()
    {
        using var client = _factory.CreateClient();
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var rows = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint =>
            {
                var path = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                    ?? ["*"];
                var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
                var authorize = endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null;
                var limiter = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                    ?? (endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null
                        ? "disabled"
                        : null);
                var allowListed = anonymous && IsApprovedAnonymousPath(path);
                return new EndpointAuditRow(path, methods, anonymous, authorize, limiter, allowListed);
            })
            .Where(row => row.Anonymous)
            .OrderBy(row => row.Path)
            .ThenBy(row => string.Join(",", row.Methods))
            .ToArray();

        var evidence = string.Join(
            Environment.NewLine,
            new[] { "Endpoint | HTTP Method | Anonymous | Authorization Metadata | Rate Limiter | Allow-listed | Result" }
                .Concat(rows.Select(row =>
                    $"| {row.Path} | {string.Join(",", row.Methods)} | {row.Anonymous} | {row.AuthorizationMetadata} | {row.RateLimiter ?? "none"} | {row.AllowListed} | {(row.AllowListed && row.RateLimiter is not null ? "PASS" : "FAIL")} |")));
        await File.WriteAllTextAsync("/tmp/ratanhr-runtime-endpoint-audit.md", evidence);

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.True(row.AllowListed, $"Anonymous endpoint is outside the approved allow-list: {row.Path}");
            Assert.False(string.IsNullOrWhiteSpace(row.RateLimiter),
                $"Anonymous endpoint has no rate-limiter policy: {row.Path}");
        });

        var fallbackProvider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = await fallbackProvider.GetFallbackPolicyAsync();
        Assert.NotNull(fallback);
        Assert.Contains(fallback!.Requirements, requirement =>
            requirement is DenyAnonymousAuthorizationRequirement);

        var protectedResponse = await client.GetAsync("/api/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    private static bool IsApprovedAnonymousPath(string path) =>
        path is "/api/auth/login"
            or "/api/auth/refresh"
            or "/api/auth/logout"
            or "/api/auth/forgot-password"
            or "/api/auth/reset-password"
            or "/api/auth/mfa/verify"
            or "/api/auth/csrf"
            or "/health"
            or "/healthz"
            or "/healthz/ready"
            or "/healthz/live"
            // RHR-015 FIX: /metrics is Prometheus's scrape target. Like the health-check
            // endpoints above, the scraper sends no JWT — it is an unauthenticated
            // infrastructure probe, not a user-facing route. Access is restricted at the
            // network layer (nginx internal-CIDR allow-list; API port never published to
            // the host in production), not via application auth. Carries the "api"
            // rate-limiter policy the same as every other allow-listed anonymous route.
            or "/metrics"
            // FIX (audit): "/" and the SPA fallback route are legitimate anonymous
            // endpoints (Program.cs) — an unauthenticated visitor must be able to reach
            // the React app shell / login page. Both now carry the "api" rate-limiter
            // policy (see Program.cs), so they satisfy the same allow-listed +
            // rate-limited invariant this test enforces for every other anonymous route.
            or "/"
            or "/{*path:nonfile}";

    private sealed record EndpointAuditRow(
        string Path,
        IReadOnlyList<string> Methods,
        bool Anonymous,
        bool AuthorizationMetadata,
        string? RateLimiter,
        bool AllowListed);
}