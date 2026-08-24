namespace HRMS.API.Security;

/// <summary>
/// FIX 3: Discriminated union for tenant context — replaces BaseController's -1 sentinel.
/// Provides type-safe representation of company scope with no magic numbers.
/// SuperAdmin = unrestricted access (IsSuperAdmin = true, CompanyId = null)
/// TenantAdmin = restricted to CompanyId (IsSuperAdmin = false, CompanyId = value)
/// Invalid = request lacks valid company claim (never used in real requests, indicates bug)
/// </summary>
public abstract record CompanyScope
{
    public sealed record SuperAdmin : CompanyScope;
    public sealed record TenantAdmin(int CompanyId) : CompanyScope;
    public sealed record Invalid : CompanyScope;

    /// <summary>
    /// Parses tenant context from JWT claims.
    /// </summary>
    /// <param name="userPrincipal">The authenticated ClaimsPrincipal from Request.User</param>
    /// <returns>SuperAdmin | TenantAdmin(companyId) | Invalid</returns>
    public static CompanyScope FromClaimsPrincipal(System.Security.Claims.ClaimsPrincipal userPrincipal)
    {
        if (userPrincipal.IsInRole(HRMS.Application.Common.AppRoles.SuperAdmin))
            return new SuperAdmin();

        if (int.TryParse(
            userPrincipal.FindFirst("companyId")?.Value,
            out var companyId) && companyId > 0)
        {
            return new TenantAdmin(companyId);
        }


        // Non-superadmin without a valid companyId claim: indicate invalid state
        return new Invalid();
    }

    /// <summary>
    /// Extract company ID for database filtering (or null for unrestricted superadmin queries).
    /// Use this in repository queries to maintain tenant isolation.
    /// </summary>
    public int? GetCompanyIdForFilter() =>
        this switch
        {
            SuperAdmin => null,
            TenantAdmin admin => admin.CompanyId,
            Invalid => -1,  // impossible PK sentinel — all WHERE company_id = -1 return empty
            _ => throw new System.NotImplementedException()
        };

    /// <summary>
    /// Check if the scope is valid (SuperAdmin or TenantAdmin with valid ID).
    /// Use this to short-circuit endpoints with 403 when claim is missing.
    /// </summary>
    public bool IsValid() => this is not Invalid;
}
