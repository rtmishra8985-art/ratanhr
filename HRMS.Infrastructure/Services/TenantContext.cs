namespace HRMS.Infrastructure.Services;

/// <summary>
/// Scoped per-request tenant context. Populated by <c>TenantMiddleware</c> in Program.cs
/// from the authenticated user's JWT "companyId" claim.
///
/// Injected into <c>ApplicationDbContext</c> so that EF Core global query filters
/// automatically scope every read query to the caller's company — without each service
/// method needing an explicit <c>.Where(x => x.CompanyId == companyId)</c> guard.
///
/// Lifecycle: registered Scoped — one instance per HTTP request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current caller's company ID.
    /// <c>null</c> means either super-admin (unrestricted across all companies) or
    /// an unauthenticated request (filters still applied; queries return nothing).
    /// </summary>
    int? CompanyId { get; set; }

    /// <summary>
    /// When <c>true</c>, global query filters are bypassed entirely.
    /// Set only for super-admin callers or internal background services
    /// that legitimately need cross-tenant data.
    /// </summary>
    bool IsSuperAdmin { get; set; }
}

/// <summary>Mutable Scoped implementation — reset per request by <c>TenantMiddleware</c>.</summary>
public sealed class TenantContext : ITenantContext
{
    public int? CompanyId  { get; set; }
    public bool IsSuperAdmin { get; set; }
}
