using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using HRMS.API.Security;

namespace HRMS.API.Controllers
{
    /// <summary>
    /// Shared base controller that exposes identity helpers derived from JWT claims.
    /// Matches the claims emitted by <c>HRMS.Infrastructure.Services.TokenService</c>.
    /// </summary>
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        /// <summary>
        /// The authenticated user's company ID extracted from the <c>companyId</c> claim.
        /// Returns <c>-1</c> when the claim is absent or unparseable.
        /// Super-admin callers carry no company restriction; use <see cref="CallerCompanyIdOrNull"/>.
        /// </summary>
        protected int CompanyId =>
            int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : -1;

        /// <summary>
        /// FIX 3: Returns a discriminated union (SuperAdmin | TenantAdmin | Invalid).
        /// Type-safe alternative to CallerCompanyIdOrNull (-1 sentinel).
        /// Use in new code; CallerCompanyIdOrNull kept for backward compatibility.
        /// </summary>
        protected CompanyScope CallerCompanyScope => CompanyScope.FromClaimsPrincipal(User);

        /// <summary>
        /// Nullable variant — <c>null</c> only for SuperAdmin (unrestricted scope).
        /// For all other roles: returns the parsed company ID when the claim is valid,
        /// or <c>-1</c> (an impossible PK sentinel) when the claim is absent/malformed.
        /// Returning -1 rather than null ensures no service query ever runs without a
        /// confirmed tenant scope — all <c>WHERE company_id = -1</c> predicates return
        /// empty results (fail-closed) instead of unscoped cross-tenant access.
        /// </summary>
        protected int? CallerCompanyIdOrNull =>
            User.IsInRole(AppRoles.SuperAdmin) ? (int?)null
            : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : (int?)-1;

        /// <summary>
        /// Returns <c>true</c> when the caller either holds the SuperAdmin role (which
        /// carries no company claim by design) or has a valid, parseable companyId claim.
        /// Use this to short-circuit a request with 403 instead of silently returning
        /// empty results when a non-SuperAdmin token is missing the company claim.
        /// </summary>
        protected bool IsCompanyClaimValid =>
            User.IsInRole(AppRoles.SuperAdmin)
            || int.TryParse(User.FindFirst("companyId")?.Value, out _);

        /// <summary>
        /// The authenticated user's internal user ID from the <c>NameIdentifier</c> claim.
        /// Returns <c>0</c> when the claim is absent or unparseable.
        /// </summary>
        protected int UserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;

        /// <summary>
        /// The authenticated user's linked employee ID string from the <c>employeeId</c> claim.
        /// May be <c>null</c> for admin-only accounts that have no employee record.
        /// </summary>
        protected string? EmployeeId =>
            User.FindFirst("employeeId")?.Value;

        /// <summary>Returns <c>true</c> when the caller holds the <c>admin</c> or <c>superadmin</c> role.</summary>
        protected bool IsPrivilegedUser =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);

        // ── Cookie helpers (shared by auth controllers) ──────────────────────

        /// <summary>
        /// Appends the JWT access token as an HttpOnly, Secure, SameSite=Strict cookie.
        /// All auth and MFA controllers must use this helper rather than returning the
        /// token in the response body to preserve XSS protection.
        /// </summary>
        // FIX MED-1 (config-driven): resolve cookie lifetime from Jwt:ExpiresInMinutes so
        // it tracks any config change without requiring a code edit.
        // Uses RequestServices (service-locator) to avoid changing every derived controller's
        // constructor — BaseController is abstract and widely inherited.
        protected void SetAccessTokenCookie(string token)
        {
            var config   = HttpContext.RequestServices
                               .GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                               as Microsoft.Extensions.Configuration.IConfiguration;
            var minutes  = config?.GetValue<double>("Jwt:ExpiresInMinutes") ?? 30;
            Response.Cookies.Append("hrms_access_token", token,
                new Microsoft.AspNetCore.Http.CookieOptions
                {
                    HttpOnly = true,
                    Secure   = IsSecureCookieContext,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                    Path     = "/",
                    Expires  = DateTimeOffset.UtcNow.AddMinutes(minutes)
                });
        }

        /// <summary>
        /// Appends the refresh token as an HttpOnly, Secure, SameSite=Strict cookie
        /// scoped to /api/auth so it's available to the refresh endpoint and logout.
        /// </summary>
        protected void SetRefreshTokenCookie(string token) =>
            Response.Cookies.Append("hrms_refresh_token", token, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure   = IsSecureCookieContext,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Path     = "/api/auth",
                Expires  = DateTimeOffset.UtcNow.AddDays(7)
            });

        /// <summary>
        /// BUG FIX: SetAccessTokenCookie/SetRefreshTokenCookie previously hardcoded
        /// Secure = true unconditionally. docker-compose.override.yml (local dev) serves
        /// the API directly over plain HTTP on localhost:8080 with no TLS termination
        /// ("No Nginx — hit the API directly"). Browsers silently drop/never send a
        /// Secure cookie over an insecure origin, so login never actually persisted a
        /// session in that setup — the exact class of bug already fixed for the
        /// antiforgery cookie in Program.cs (see the SecurePolicy comment there), but
        /// missed here for the real auth cookies. Mirror the same environment-aware
        /// policy: Secure is mandatory outside Development, and only relaxed for local
        /// HTTP development.
        /// </summary>
        protected bool IsSecureCookieContext
        {
            get
            {
                // NULL-SAFETY FIX: RequestServices itself can be null (unit tests using a bare
                // DefaultHttpContext with no configured service provider, or any minimal hosting
                // context). The original code only null-checked the *resolved* service, not
                // RequestServices, so this threw NullReferenceException from Logout() whenever a
                // test or lightweight caller built a HttpContext without an IServiceProvider —
                // exactly what AuthenticationControllerSecurityTests does. When RequestServices is
                // unavailable we cannot verify the environment, so fail closed (Secure = true) —
                // safe by default, and correct for the two known callers in production, which
                // always have RequestServices via the real ASP.NET Core pipeline.
                var env = HttpContext.RequestServices?
                    .GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment))
                    as Microsoft.Extensions.Hosting.IHostEnvironment;
                return env == null || !env.IsDevelopment() || Request.IsHttps;
            }
        }
    }
}
