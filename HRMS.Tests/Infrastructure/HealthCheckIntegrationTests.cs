// Infrastructure-layer integration tests for the four health-check HTTP endpoints.
//
// Uses Microsoft.AspNetCore.Mvc.Testing with a minimal in-process WebApplication
// (no real PostgreSQL, Redis, or Hangfire required) so the tests run in any CI
// environment without live infrastructure.
//
// The server mirrors the exact MapHealthChecks() configuration from Program.cs,
// including the shared HealthCheckResponseWriter introduced by FIX 4.
//
// The [Collection("WebApp")] attribute causes xUnit to share one WebAppHealthFixture
// instance across all tests in this class, saving startup cost.

using HRMS.API.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text.Json;
using Xunit;

namespace HRMS.Tests.Infrastructure;

// ── Shared fixture ────────────────────────────────────────────────────────────

/// <summary>
/// Shared fixture: creates one minimal in-process HTTP server per test collection.
/// Registers the same health checks and MapHealthChecks() predicates as Program.cs
/// (database tagged "ready", redis tagged "ready", email untagged) using always-Healthy
/// stubs so no live infrastructure is required.
/// </summary>
public sealed class WebAppHealthFixture : IDisposable
{
    private readonly WebApplication _app;
    public HttpClient Client { get; }

    public WebAppHealthFixture()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*"
        });

        builder.Services.AddHealthChecks()
            .AddCheck("database",
                () => HealthCheckResult.Healthy("stub — no real DB in tests"),
                tags: ["db", "ready"])
            .AddCheck("redis",
                () => HealthCheckResult.Healthy("stub — no real Redis in tests"),
                tags: ["cache", "ratelimit", "ready"])
            .AddCheck("email",
                () => HealthCheckResult.Healthy("stub — SMTP not configured"));

        _app = builder.Build();

        // Mirror Program.cs health-check endpoint registration exactly.
        // FIX 4: /health and /healthz now delegate to the shared HealthCheckResponseWriter.
        _app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
        });

        _app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
        });

        _app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        _app.MapHealthChecks("/healthz/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        _app.StartAsync().GetAwaiter().GetResult();
        Client = _app.GetTestClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        _app.DisposeAsync().GetAwaiter().GetResult();
    }
}

// ── Collection definition ─────────────────────────────────────────────────────

[CollectionDefinition("WebApp")]
public class WebAppCollection : ICollectionFixture<WebAppHealthFixture> { }

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Integration tests for the four health-check HTTP endpoints.
/// All tests share one <see cref="WebAppHealthFixture"/> server instance via
/// the "WebApp" xUnit collection to minimise startup overhead.
/// </summary>
[Collection("WebApp")]
public class HealthCheckIntegrationTests
{
    private readonly HttpClient _client;

    public HealthCheckIntegrationTests(WebAppHealthFixture fixture)
    {
        _client = fixture.Client;
    }

    // ── /health ───────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /health must return HTTP 200 with Content-Type application/json
    /// and a JSON body containing a top-level "status" field.
    /// Verifies HealthCheckResponseWriter produces the expected shape.
    /// </summary>
    [Fact]
    public async Task Health_ReturnsHttp200_WithJsonContentType_AndStatusField()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        Assert.StartsWith("application/json", contentType, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("status", out _),
            $"Expected 'status' field in /health JSON body. Actual: {body}");
    }

    // ── /healthz ──────────────────────────────────────────────────────────────

    /// <summary>GET /healthz must return HTTP 200 (all stub checks are Healthy).</summary>
    [Fact]
    public async Task Healthz_ReturnsHttp200()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── /healthz/ready ────────────────────────────────────────────────────────

    /// <summary>
    /// GET /healthz/ready must return HTTP 200.
    /// Only checks tagged "ready" run; both database and redis stubs are Healthy.
    /// </summary>
    [Fact]
    public async Task HealthzReady_ReturnsHttp200()
    {
        var response = await _client.GetAsync("/healthz/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── /healthz/live ─────────────────────────────────────────────────────────

    /// <summary>
    /// GET /healthz/live must return HTTP 200.
    /// Predicate = _ => false means zero checks run, so the result is always Healthy.
    /// This models the Kubernetes liveness probe semantic: "is the process alive?"
    /// </summary>
    [Fact]
    public async Task HealthzLive_ReturnsHttp200()
    {
        var response = await _client.GetAsync("/healthz/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
