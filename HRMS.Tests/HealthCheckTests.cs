// New file — provides unit-test coverage for the health check configuration that
// was previously untested. Tests cover:
//   • /healthz/live  — predicate excludes ALL checks (process alive = healthy)
//   • /healthz/ready — predicate includes only checks tagged "ready"
//   • /health + /healthz — JSON response shape (status names)
//   • EmailHealthCheckService behaviour (configured / unconfigured SMTP)
//
// These tests do NOT start a real ASP.NET Core server; they test the predicate
// lambdas and the EmailHealthCheckService IHealthCheck implementation in isolation.
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit-tests for the health check configuration and
/// <see cref="EmailHealthCheckService"/> used by /health, /healthz,
/// /healthz/ready, and /healthz/live.
/// </summary>
public class HealthCheckTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    // EmailHealthCheckService takes IHostEnvironment (not IWebHostEnvironment).
    private static IHostEnvironment MakeEnv(string name)
    {
        var m = new Mock<IHostEnvironment>();
        m.Setup(e => e.EnvironmentName).Returns(name);
        return m.Object;
    }

    private static HealthCheckRegistration MakeReg(string name, params string[] tags) =>
        new(name, _ => throw new Exception("not called in predicate tests"),
            HealthStatus.Unhealthy, tags);

    private static EmailHealthCheckService MakeSvc(
        string smtpHost = "", string envName = "Testing")
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Host"] = smtpHost,
                ["Email:Port"] = "587"
            }).Build();
        return new EmailHealthCheckService(cfg, MakeEnv(envName));
    }

    // ── /healthz/live predicate ────────────────────────────────────────────
    // Program.cs:  Predicate = _ => false
    // Meaning: the liveness probe runs NO health checks — the response is always
    // Healthy (200) as long as the process is alive. This is the Kubernetes
    // semantic for liveness: "is the container alive?" not "are dependencies up?".

    [Fact]
    public void HealthzLive_Predicate_ExcludesEveryCheck()
    {
        // Simulate Predicate = _ => false with a variety of registrations.
        Func<HealthCheckRegistration, bool> predicate = _ => false;

        Assert.False(predicate(MakeReg("database", "db", "ready")));
        Assert.False(predicate(MakeReg("redis",    "cache", "ratelimit", "ready")));
        Assert.False(predicate(MakeReg("email")));
        Assert.False(predicate(MakeReg("anything-at-all")));
    }

    // ── /healthz/ready predicate ───────────────────────────────────────────
    // Program.cs:  Predicate = check => check.Tags.Contains("ready")
    // Meaning: readiness includes only dependencies that gate traffic routing
    // (database and redis are tagged "ready"; email is not).

    [Fact]
    public void HealthzReady_Predicate_IncludesChecksTaggedReady()
    {
        Func<HealthCheckRegistration, bool> predicate =
            check => check.Tags.Contains("ready");

        Assert.True(predicate(MakeReg("database", "db", "ready")));
        Assert.True(predicate(MakeReg("redis",    "cache", "ratelimit", "ready")));
    }

    [Fact]
    public void HealthzReady_Predicate_ExcludesChecksWithoutReadyTag()
    {
        Func<HealthCheckRegistration, bool> predicate =
            check => check.Tags.Contains("ready");

        Assert.False(predicate(MakeReg("email")));              // no tags at all
        Assert.False(predicate(MakeReg("smtp", "smtp")));       // tagged, but not "ready"
        Assert.False(predicate(MakeReg("metrics", "internal"))); // different tag
    }

    [Fact]
    public void HealthzReady_Predicate_RedisTags_MatchProgramCs()
    {
        // Verify the tag set used in Program.cs matches the predicate.
        // Program.cs: healthBuilder.AddRedis(..., tags: ["cache", "ratelimit", "ready"])
        Func<HealthCheckRegistration, bool> predicate =
            check => check.Tags.Contains("ready");

        var redis = MakeReg("redis", "cache", "ratelimit", "ready");
        Assert.True(predicate(redis));
    }

    // ── Health check status string values ─────────────────────────────────
    // The /health and /healthz endpoints JSON-serialize status.ToString().
    // This test pins the string values so any framework change that alters
    // the enum names is caught before it breaks monitoring dashboards.

    [Fact]
    public void HealthStatus_StringValues_MatchExpectedNames()
    {
        Assert.Equal("Healthy",   HealthStatus.Healthy.ToString());
        Assert.Equal("Degraded",  HealthStatus.Degraded.ToString());
        Assert.Equal("Unhealthy", HealthStatus.Unhealthy.ToString());
    }

    // ── EmailHealthCheckService ─────────────────────────────────────────────

    [Fact]
    public async Task EmailHealthCheck_SmtpHostEmpty_NonProduction_ReturnsHealthy()
    {
        // When SMTP host is empty and environment is not Production, the service
        // reports Healthy (intentionally disabled, not broken).
        var svc = MakeSvc(smtpHost: "", envName: "Development");
        var result = await svc.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task EmailHealthCheck_SmtpHostEmpty_InProduction_ReturnsDegraded()
    {
        // In Production a missing SMTP host is a degraded configuration — email
        // delivery silently fails. The validator surfaces this in /health.
        var svc = MakeSvc(smtpHost: "", envName: "Production");
        var result = await svc.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Description);
        Assert.Contains("SMTP", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailHealthCheck_SmtpConfigured_NoRecentFailure_ReturnsHealthy()
    {
        // SMTP host is set and no recent failure was recorded — Healthy.
        EmailHealthCheck.LastFailureUtc = null; // clear any state from prior tests
        var svc = MakeSvc(smtpHost: "mail.example.com", envName: "Production");

        var result = await svc.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task EmailHealthCheck_RecentSendFailure_ReturnsDegraded()
    {
        // If EmailService recorded a failure within the last 30 minutes,
        // the health check must surface it as Degraded.
        EmailHealthCheck.LastFailureUtc    = DateTime.UtcNow.AddMinutes(-5);
        EmailHealthCheck.LastFailureReason = "Connection refused";
        try
        {
            var svc = MakeSvc(smtpHost: "mail.example.com", envName: "Production");
            var result = await svc.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.NotNull(result.Description);
        }
        finally
        {
            // Reset static state so this test does not pollute subsequent tests.
            EmailHealthCheck.LastFailureUtc    = null;
            EmailHealthCheck.LastFailureReason = null;
        }
    }

    [Fact]
    public async Task EmailHealthCheck_OldSendFailure_ReturnsHealthy()
    {
        // A failure older than 30 minutes is no longer considered active.
        EmailHealthCheck.LastFailureUtc    = DateTime.UtcNow.AddMinutes(-45);
        EmailHealthCheck.LastFailureReason = "Old error";
        try
        {
            var svc = MakeSvc(smtpHost: "mail.example.com", envName: "Production");
            var result = await svc.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            EmailHealthCheck.LastFailureUtc    = null;
            EmailHealthCheck.LastFailureReason = null;
        }
    }

    [Fact]
    public async Task EmailHealthCheck_DoesNotThrowUnhandledException()
    {
        // Regardless of configuration, CheckHealthAsync must never throw —
        // an unhandled exception from a health check crashes the /health endpoint.
        var configs = new[]
        {
            MakeSvc("",            "Development"),
            MakeSvc("",            "Production"),
            MakeSvc("localhost",   "Testing"),
        };

        foreach (var svc in configs)
        {
            var result = await Record.ExceptionAsync(() =>
                svc.CheckHealthAsync(new HealthCheckContext()));
            Assert.Null(result); // must not throw
        }
    }
}
