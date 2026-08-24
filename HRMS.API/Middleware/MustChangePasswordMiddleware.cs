namespace HRMS.API.Middleware;

/// <summary>
/// Enforces a mandatory password change before the user can access any protected resource.
///
/// When the authenticated user's JWT contains a "mustChangePassword=true" claim, every
/// request is blocked with 403 Forbidden except for the explicitly allowed passthrough paths
/// (change-password, logout, refresh, login, Swagger, health, and metrics endpoints).
///
/// This middleware must be registered AFTER UseAuthentication() and UseAuthorization() so
/// that the JWT has already been validated and the ClaimsPrincipal is populated.
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    // Paths that are allowed even when MustChangePassword = true.
    // Add any additional public/passthrough paths here if needed.
    private static readonly string[] AllowedPaths =
    {
        "/api/auth/change-password",
        "/api/auth/csrf",       // FIX: CSRF token seed must be reachable to obtain the
                                // X-XSRF-TOKEN needed to call change-password itself.
                                // Without this, the double-submit CSRF pattern creates
                                // an unresolvable catch-22 for users with mustChangePassword=true.
        "/api/auth/logout",
        "/api/auth/refresh",
        "/api/auth/login",
        "/swagger",
        "/health",
        "/metrics"
    };

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only enforce for authenticated users.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChange = context.User.FindFirst("mustChangePassword")?.Value;
            if (mustChange == "true")
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var allowed = AllowedPaths.Any(p =>
                    path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                if (!allowed)
                {
                    context.Response.StatusCode  = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        """{"success":false,"message":"Password change required. Please update your password before continuing.","mustChangePassword":true}""");
                    return;
                }
            }
        }

        await _next(context);
    }
}
