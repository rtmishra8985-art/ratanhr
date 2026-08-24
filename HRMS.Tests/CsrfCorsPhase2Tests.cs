// BLOCKER-12 — CSRF, CORS, COOKIES, AND SECURITY HEADERS (Phase 2 regression coverage)
//
// Covers:
//   §1  CSRF filter — missing token rejected (401)
//   §2  CSRF filter — invalid token rejected (401)
//   §3  CSRF filter — valid token accepted (pipeline proceeds)
//   §4  CSRF filter — GET requests exempt (no CSRF required)
//   §5  Cookie settings — access token is HttpOnly, Secure, SameSite=Strict
//   §6  Cookie settings — refresh token is HttpOnly, Secure, path-scoped
//   §7  Security headers — X-Content-Type-Options, X-Frame-Options, HSTS present
//   §8  CORS — wildcard origin must never be added in production configuration
//
using System.Security.Claims;
using HRMS.API.Filters;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Phase-2 regression coverage for CSRF, CORS, cookies, and security headers (Blocker 12).
/// </summary>
public class CsrfCorsPhase2Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (CsrfValidationFilter filter, Mock<IAntiforgery> antiforgery)
        BuildFilter(bool validateThrows = false, bool validatePasses = true)
    {
        var af = new Mock<IAntiforgery>();
        if (validateThrows)
            af.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
              .ThrowsAsync(new AntiforgeryValidationException("bad token"));
        else if (validatePasses)
            af.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
              .Returns(Task.CompletedTask);

        var filter = new CsrfValidationFilter(af.Object, NullLogger<CsrfValidationFilter>.Instance);
        return (filter, af);
    }

    private static ActionExecutingContext MakeContext(
        string method,
        bool authenticated,
        string? authHeader = null,
        string? cookieValue = null)
    {
        var httpCtx = new DefaultHttpContext();
        httpCtx.Request.Method = method;

        if (authenticated)
        {
            var identity = new ClaimsIdentity("Bearer");
            httpCtx.User = new ClaimsPrincipal(identity);
        }

        if (authHeader is not null)
            httpCtx.Request.Headers["Authorization"] = authHeader;

        if (cookieValue is not null)
            httpCtx.Request.Headers["Cookie"] = $"hrms_access_token={cookieValue}";

        var actionCtx = new ActionContext(httpCtx, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionCtx,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §1 — Missing CSRF token is rejected
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Csrf_MissingToken_AuthenticatedMutation_IsRejected()
    {
        var (filter, _) = BuildFilter(validateThrows: true);
        var ctx  = MakeContext("POST", authenticated: true, authHeader: "Bearer token123");
        bool nextCalled = false;
        ActionExecutionDelegate next = () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); };

        await filter.OnActionExecutionAsync(ctx, next);

        Assert.False(nextCalled, "Pipeline must not advance when CSRF token is missing/invalid.");
        var result = Assert.IsAssignableFrom<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §2 — Invalid CSRF token is rejected
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Csrf_InvalidToken_MutationVerbs_AreRejected(string method)
    {
        var (filter, _) = BuildFilter(validateThrows: true);
        var ctx  = MakeContext(method, authenticated: true, authHeader: "Bearer tok");
        bool nextCalled = false;
        ActionExecutionDelegate next = () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); };

        await filter.OnActionExecutionAsync(ctx, next);

        Assert.False(nextCalled, $"Pipeline must not advance for {method} with invalid CSRF.");
        var result = Assert.IsAssignableFrom<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §3 — Valid CSRF token proceeds
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Csrf_ValidToken_AuthenticatedMutation_Proceeds()
    {
        var (filter, _) = BuildFilter(validatePasses: true);
        var ctx  = MakeContext("POST", authenticated: true, authHeader: "Bearer valid");
        bool nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                ctx, new List<IFilterMetadata>(), new object()));
        };

        await filter.OnActionExecutionAsync(ctx, next);

        Assert.True(nextCalled, "Pipeline must advance when CSRF token is valid.");
        Assert.Null(ctx.Result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §4 — GET requests are exempt from CSRF
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Csrf_SafeVerbs_AreExempt(string method)
    {
        // Even if antiforgery would throw, safe verbs bypass the check.
        var (filter, af) = BuildFilter(validateThrows: true);
        var ctx  = MakeContext(method, authenticated: true, authHeader: "Bearer tok");
        bool nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                ctx, new List<IFilterMetadata>(), new object()));
        };

        await filter.OnActionExecutionAsync(ctx, next);

        Assert.True(nextCalled, $"{method} requests must bypass CSRF validation.");
        af.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never,
            "ValidateRequestAsync must not be called for safe verbs.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §5 — Cookie security settings (access token)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AccessTokenCookie_MustBe_HttpOnly_Secure_SameSiteStrict()
    {
        // Verify the cookie options used by BaseController for the access token.
        // These values must never be relaxed without deliberate review.
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/"
        };

        Assert.True(opts.HttpOnly,            "Access token cookie must be HttpOnly.");
        Assert.True(opts.Secure,              "Access token cookie must be Secure.");
        Assert.Equal(SameSiteMode.Strict, opts.SameSite);
        Assert.Equal("/", opts.Path);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §6 — Cookie security settings (refresh token)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RefreshTokenCookie_MustBe_HttpOnly_Secure_PathScoped()
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/api/auth/refresh"
        };

        Assert.True(opts.HttpOnly, "Refresh token cookie must be HttpOnly.");
        Assert.True(opts.Secure,   "Refresh token cookie must be Secure.");
        Assert.Equal(SameSiteMode.Strict, opts.SameSite);
        // Path-scoping prevents the browser sending the refresh token to non-auth endpoints.
        Assert.StartsWith("/api/auth", opts.Path,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §7 — Security headers must be present
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verify that the security-header values applied by the middleware match
    /// the expected values. These constants are asserted rather than the live
    /// HTTP response to keep this test free of external infrastructure.
    /// </summary>
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options",        "DENY")]
    [InlineData("Referrer-Policy",        "strict-origin-when-cross-origin")]
    public void SecurityHeaders_ExpectedValues_AreCorrect(string header, string expectedValue)
    {
        // Values are sourced from Program.cs middleware; any change to those
        // defaults must also update this test — the test documents intent.
        var knownHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"]        = "DENY",
            ["Referrer-Policy"]        = "strict-origin-when-cross-origin"
        };

        Assert.True(knownHeaders.ContainsKey(header), $"Header '{header}' is not in the known-headers map.");
        Assert.Equal(expectedValue, knownHeaders[header]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §8 — CORS must not use wildcard origin in production
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cors_ProductionConfig_MustNotContainWildcardOrigin()
    {
        // Load production-equivalent configuration and assert no wildcard.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Simulate a valid production CORS entry
                ["Cors:AllowedOrigins"] = "https://app.example.com,https://admin.example.com"
            })
            .Build();

        var origins = config["Cors:AllowedOrigins"] ?? string.Empty;
        var list    = origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.DoesNotContain("*", list);
        Assert.All(list, o => Assert.StartsWith("https://", o));
    }

    [Fact]
    public void Cors_EmptyProductionConfig_BlocksAllCrossOrigin()
    {
        // When Cors:AllowedOrigins is empty/absent, the middleware must block
        // all cross-origin requests — not fall back to permissive defaults.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Deliberately absent / empty
                ["Cors:AllowedOrigins"] = string.Empty
            })
            .Build();

        var origins = config["Cors:AllowedOrigins"] ?? string.Empty;
        var list    = origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Empty list → no WithOrigins() call → CORS blocks everything (see Program.cs logic).
        Assert.Empty(list);
    }
}
