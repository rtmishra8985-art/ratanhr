using Hangfire.Dashboard;

namespace HRMS.API.Security;

/// <summary>
/// M-21: Restricts Hangfire dashboard access to authenticated superadmins only.
/// Unauthenticated callers receive 401; non-superadmins receive 403.
/// </summary>
public class HangfireSuperAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true
            && http.User.IsInRole(AppRoles.SuperAdmin);
    }
}
