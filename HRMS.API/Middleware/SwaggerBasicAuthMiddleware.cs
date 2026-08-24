using System.Text;

namespace HRMS.API.Middleware;

/// <summary>
/// HTTP Basic Auth middleware that guards the /swagger UI.
///
/// SECURITY POLICY (P3-01 fix):
///   - In Development: if Swagger:Username / Swagger:Password are not configured, the
///     middleware passes through (allows unauthenticated access on localhost only).
///   - In all other environments (Staging, etc.): if credentials are not configured
///     the middleware returns 403 Forbidden and logs a startup warning. Swagger must
///     always be credential-protected outside local development.
///
/// To configure:  Swagger:Username and Swagger:Password in appsettings / environment secrets.
/// To disable:    Remove AppSettings:EnableSwagger=true from the environment config.
/// </summary>
public class SwaggerBasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private readonly ILogger<SwaggerBasicAuthMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public SwaggerBasicAuthMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ILogger<SwaggerBasicAuthMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next   = next;
        _config = config;
        _logger = logger;
        _env    = env;

        // Warn loudly at startup when Swagger is enabled without credentials
        // so the problem is caught in logs before the first request.
        var user = _config["Swagger:Username"];
        var pass = _config["Swagger:Password"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogWarning(
                    "Swagger is enabled without credentials (Swagger:Username / Swagger:Password not set). " +
                    "Unauthenticated access is permitted in Development only. " +
                    "Set credentials before deploying to any shared environment.");
            }
            else
            {
                _logger.LogError(
                    "SECURITY: Swagger is enabled in a non-Development environment but " +
                    "Swagger:Username / Swagger:Password are not configured. " +
                    "All /swagger requests will be rejected with 403 until credentials are set. " +
                    "Either configure credentials or disable Swagger (remove AppSettings:EnableSwagger=true).");
            }
        }
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(ctx);
            return;
        }

        var user = _config["Swagger:Username"];
        var pass = _config["Swagger:Password"];
        var credentialsConfigured = !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass);

        // In non-Development environments, unconfigured credentials = hard deny.
        if (!credentialsConfigured && !_env.IsDevelopment())
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync(
                "Swagger access is disabled. Configure Swagger:Username and Swagger:Password to enable.");
            return;
        }

        // If credentials are configured, enforce Basic Auth regardless of environment.
        if (credentialsConfigured)
        {
            if (!ctx.Request.Headers.TryGetValue("Authorization", out var authHeader)
                || !authHeader.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"HRMS Swagger\"";
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("Swagger access requires authentication.");
                return;
            }

            try
            {
                var encoded = authHeader.ToString()["Basic ".Length..].Trim();
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                var parts   = decoded.Split(':', 2);

                if (parts.Length != 2 || parts[0] != user || parts[1] != pass)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsync("Invalid Swagger credentials.");
                    return;
                }
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex,
                    "Swagger auth received a malformed Base64 Authorization header");
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync("Malformed Authorization header.");
                return;
            }
        }

        // Development with no credentials configured: pass through (localhost only).
        await _next(ctx);
    }
}
