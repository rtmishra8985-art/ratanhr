// FIX 5 — CSRF token endpoint regression coverage.
//
// Documents and locks in the correct double-submit CSRF pattern described in
// Program.cs's "/api/auth/csrf" comment block:
//   - Exactly ONE XSRF-TOKEN cookie is set (the framework's CookieToken).
//   - The JSON response body carries the separate RequestToken value.
//   - The cookie carries the expected security attributes (Secure, SameSite=Strict,
//     HttpOnly=false so client-side JS can read it and echo it back as a header).
//
// This guards against the previously-fixed bug where a second Set-Cookie header
// (RequestToken) overwrote the framework's CookieToken cookie, which made every
// subsequent mutating request fail CSRF validation.
//
// RHR-016 FIX: GET /api/auth/csrf is now [AllowAnonymous] (Program.cs). It MUST be
// reachable before login: the SPA seeds the XSRF-TOKEN cookie on page mount, before
// any authenticated session exists, so the double-submit cookie is available for the
// very first login attempt and for logout immediately afterward. Requiring
// authentication here (the previous behaviour) meant the seed call always 401'd
// silently, the XSRF-TOKEN cookie was never set, and every subsequent mutating
// request — most visibly Logout, then any later Login once a stale
// hrms_access_token cookie triggered the CSRF filter — failed with
// "CSRF token missing or invalid", permanently locking users out after one
// logout. Reuses the HrmsTestWebAppFactory + test auth scheme already established
// by EmployeeSelfControllerIdorIntegrationTests so this class needs no live
// database, Redis, or SMTP server.
using System.Security.Claims;
using System.Text.Json;
using HRMS.Tests.Security;
using Xunit;

namespace HRMS.Tests.Integration.Auth;

public class CsrfTokenEndpointTests : IClassFixture<HrmsTestWebAppFactory>
{
    private readonly HttpClient _client;

    public CsrfTokenEndpointTests(HrmsTestWebAppFactory factory)
    {
        // Cookies must not be auto-collected into a hidden CookieContainer —
        // the tests inspect the raw Set-Cookie header themselves.
        //
        // BaseAddress uses https:// so TestServer marks HttpContext.Request.IsHttps = true.
        // AddAntiforgery() is configured with Cookie.SecurePolicy = CookieSecurePolicy.Always,
        // which throws InvalidOperationException on a plain-HTTP request — matching the
        // production requirement that this endpoint only ever be reached over TLS.
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static string BuildAuthHeader(string role = "employee", int companyId = 1, int userId = 1)
    {
        var claims = new List<object>
        {
            new { type = ClaimTypes.NameIdentifier, value = userId.ToString() },
            new { type = ClaimTypes.Role,            value = role },
            new { type = "companyId",                value = companyId.ToString() },
        };
        var json = JsonSerializer.Serialize(claims);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private HttpRequestMessage AuthenticatedGet(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Test-Claims", BuildAuthHeader());
        return req;
    }

    private static List<string> GetSetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToList()
            : new List<string>();

    [Fact]
    public async Task GetCsrfToken_Unauthenticated_Returns200_AndIsAllowAnonymous()
    {
        // RHR-016 FIX: no X-Test-Claims header → anonymous. The endpoint is now
        // [AllowAnonymous] by design — it must be callable before login so the SPA
        // can seed the XSRF-TOKEN cookie on page mount, before any session exists.
        var response = await _client.GetAsync("/api/auth/csrf");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCsrfToken_Returns_RequestToken_InBody()
    {
        var response = await _client.SendAsync(AuthenticatedGet("/api/auth/csrf"));

        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK,
            $"Expected 200 OK, got {response.StatusCode}. Body: {content}");

        using var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("requestToken", out var requestToken));
        Assert.NotEqual(JsonValueKind.Null, requestToken.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(requestToken.GetString()));

        Assert.True(root.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());
    }

    [Fact]
    public async Task GetCsrfToken_Sets_Exactly_One_XsrfToken_Cookie()
    {
        var response = await _client.SendAsync(AuthenticatedGet("/api/auth/csrf"));

        var setCookieHeaders = GetSetCookieHeaders(response);
        Assert.NotEmpty(setCookieHeaders);

        // CRITICAL: only ONE Set-Cookie header may target XSRF-TOKEN.
        // If this ever becomes 2, the double-cookie regression has returned.
        var xsrfTokenCookies = setCookieHeaders
            .Where(c => c.StartsWith("XSRF-TOKEN=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(xsrfTokenCookies);

        var cookieValue = xsrfTokenCookies[0].Split(';')[0].Split('=', 2)[1];
        Assert.False(string.IsNullOrWhiteSpace(cookieValue));
    }

    [Fact]
    public async Task GetCsrfToken_Cookie_Has_Expected_Security_Attributes()
    {
        var response = await _client.SendAsync(AuthenticatedGet("/api/auth/csrf"));

        var xsrfTokenCookie = GetSetCookieHeaders(response)
            .First(c => c.StartsWith("XSRF-TOKEN=", StringComparison.OrdinalIgnoreCase));

        // JS must be able to read this cookie to echo it back as X-XSRF-TOKEN,
        // so it is deliberately NOT HttpOnly.
        Assert.DoesNotContain("httponly", xsrfTokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", xsrfTokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", xsrfTokenCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCsrfToken_RequestToken_And_CookieToken_AreBothPresent()
    {
        // The double-submit pattern relies on RequestToken (body) and CookieToken
        // (cookie) being the two distinct halves of the same antiforgery token pair —
        // they are not expected to be equal, but both must be present and non-empty.
        var response = await _client.SendAsync(AuthenticatedGet("/api/auth/csrf"));

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        var requestToken = json.RootElement.GetProperty("requestToken").GetString();

        var xsrfTokenCookie = GetSetCookieHeaders(response)
            .First(c => c.StartsWith("XSRF-TOKEN=", StringComparison.OrdinalIgnoreCase));
        var cookieToken = xsrfTokenCookie.Split(';')[0].Split('=', 2)[1];

        Assert.False(string.IsNullOrWhiteSpace(requestToken));
        Assert.False(string.IsNullOrWhiteSpace(cookieToken));
    }
}
