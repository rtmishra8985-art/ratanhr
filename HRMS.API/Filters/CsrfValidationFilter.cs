using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HRMS.API.Filters;

/// <summary>
/// Validates the X-XSRF-TOKEN header on all state-changing requests that are
/// authenticated — either via an Authorization header (Bearer token in localStorage)
/// or via the hrms_access_token HttpOnly cookie (the actual auth mechanism used by
/// this application).
///
/// FIX D: The original filter only checked for an Authorization header. Because JWTs
/// are stored in HttpOnly cookies and sent automatically by the browser, authenticated
/// SPA requests never include an Authorization header — so the CSRF filter never fired
/// and all cookie-authenticated POST/PUT/PATCH/DELETE requests bypassed CSRF validation.
/// The fix adds a cookie presence check so cookie-authenticated requests are also guarded.
/// </summary>
public sealed class CsrfValidationFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> _safeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    // The HttpOnly access-token cookie set by AuthController / MfaController.
    private const string AccessTokenCookie = "hrms_access_token";

    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<CsrfValidationFilter> _logger;

    public CsrfValidationFilter(IAntiforgery antiforgery, ILogger<CsrfValidationFilter> logger)
    {
        _antiforgery = antiforgery;
        _logger      = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var req = context.HttpContext.Request;

        // Trigger on any mutating request that is authenticated — either via the
        // Authorization header (future / non-browser clients) or the HttpOnly JWT cookie
        // (current SPA flow). Anonymous endpoints carry neither and are exempt.
        bool isAuthenticated = req.Headers.ContainsKey("Authorization")
                            || req.Cookies.ContainsKey(AccessTokenCookie);

        if (!_safeMethods.Contains(req.Method) && isAuthenticated)
        {
            try
            {
                await _antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException ex)
            {
                _logger.LogWarning(ex, "CSRF validation failed for {Method} {Path}", req.Method, req.Path);
                context.Result = new UnauthorizedObjectResult(
                    new { success = false, message = "CSRF token missing or invalid. Please refresh the page and try again." });
                return;
            }
        }

        await next();
    }
}
