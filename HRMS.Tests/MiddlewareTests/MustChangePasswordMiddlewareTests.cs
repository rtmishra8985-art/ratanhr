using FluentAssertions;
using HRMS.API.Middleware;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace HRMS.Tests.MiddlewareTests;

/// <summary>
/// Tests for MustChangePasswordMiddleware: verifies that users flagged with
/// mustChangePassword=true are blocked from all endpoints except the
/// change-password route.
/// </summary>
public class MustChangePasswordMiddlewareTests
{
    private static HttpContext BuildHttpContext(
        string path,
        bool mustChangePassword,
        bool isAuthenticated = true)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Path = path;

        if (isAuthenticated)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "user-123"),
                new("mustChangePassword", mustChangePassword ? "true" : "false")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            ctx.User = new ClaimsPrincipal(identity);
        }

        return ctx;
    }

    // ─── Blocked paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MustChangePassword_True_BlocksRegularEndpoints()
    {
        // Arrange
        var ctx = BuildHttpContext("/api/employees", mustChangePassword: true);
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var sut = new MustChangePasswordMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeFalse("users with mustChangePassword=true must be blocked");
        ctx.Response.StatusCode.Should().Be(403,
            "blocked requests must return 403 Forbidden");
    }

    [Theory]
    [InlineData("/api/payroll/generate")]
    [InlineData("/api/employees")]
    [InlineData("/api/leave/apply")]
    [InlineData("/api/reports")]
    public async Task MustChangePassword_True_BlocksAllNonPasswordEndpoints(string path)
    {
        // Arrange
        var ctx = BuildHttpContext(path, mustChangePassword: true);
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var sut = new MustChangePasswordMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeFalse($"path {path} must be blocked when mustChangePassword=true");
    }

    // ─── Allowed paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MustChangePassword_True_AllowsChangePasswordEndpoint()
    {
        // Arrange
        var ctx = BuildHttpContext("/api/auth/change-password", mustChangePassword: true);
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var sut = new MustChangePasswordMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeTrue("change-password endpoint must be allowed even when flag is set");
    }

    // ─── Normal users ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MustChangePassword_False_PassesThrough()
    {
        // Arrange
        var ctx = BuildHttpContext("/api/employees", mustChangePassword: false);
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var sut = new MustChangePasswordMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeTrue("normal users must not be blocked");
    }

    // ─── Unauthenticated users ────────────────────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_User_PassesThrough()
    {
        // Arrange — unauthenticated; let the auth middleware handle it
        var ctx = BuildHttpContext("/api/employees", mustChangePassword: false, isAuthenticated: false);
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var sut = new MustChangePasswordMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeTrue("unauthenticated requests must be passed to auth middleware");
    }

    // ─── Response body ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MustChangePassword_True_ResponseBodyExplainsReason()
    {
        // Arrange
        var ctx = BuildHttpContext("/api/employees", mustChangePassword: true);
        RequestDelegate next = _ => Task.CompletedTask;
        var sut = new MustChangePasswordMiddleware(next);

        // Act
        await sut.InvokeAsync(ctx);

        // Assert
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().NotBeNullOrWhiteSpace("blocked response must include an explanatory message");
    }
}
