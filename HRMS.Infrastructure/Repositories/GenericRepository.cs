// Fix 1: Tenant-scoped repository.
// GetByIdAsync now validates ICompanyOwned entities against ITenantContext after FindAsync,
// because EF Core FindAsync bypasses global query filters.
// GetAllAsync and FindAsync rely on ApplicationDbContext global query filters (already configured).
using System.Linq.Expressions;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Data;

namespace HRMS.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _ctx;
    protected readonly DbSet<T> _set;
    private readonly ITenantContext? _tenant;

    public GenericRepository(ApplicationDbContext ctx, ITenantContext? tenant = null)
    {
        _ctx   = ctx;
        _set   = ctx.Set<T>();
        _tenant = tenant;
    }

    /// <summary>
    /// Retrieves an entity by its primary key and enforces tenant isolation for
    /// ICompanyOwned entities. FindAsync bypasses EF Core global query filters, so
    /// we apply the check manually after the lookup.
    /// </summary>
    public async Task<T?> GetByIdAsync(int id)
    {
        var entity = await _set.FindAsync(id);
        if (entity == null) return null;

        // Soft-delete guard: FindAsync also bypasses any HasQueryFilter that excludes
        // IsDeleted rows (e.g. User: HasQueryFilter(u => !u.IsDeleted)). T is generic here,
        // so we re-check via reflection rather than requiring every entity to implement
        // a shared interface. This is a no-op for entity types with no IsDeleted property.
        var isDeletedProp = typeof(T).GetProperty("IsDeleted");
        if (isDeletedProp != null
            && isDeletedProp.PropertyType == typeof(bool)
            && (bool)(isDeletedProp.GetValue(entity) ?? false))
        {
            return null;
        }

        // Tenant guard for company-scoped entities.
        // Superadmins (_tenant.IsSuperAdmin) and design-time contexts (_tenant == null)
        // bypass the check; all other callers are restricted to their own company.
        if (entity is ICompanyOwned owned
            && _tenant != null
            && !_tenant.IsSuperAdmin
            && _tenant.CompanyId.HasValue)
        {
            // Null CompanyId on the entity = global record (visible to all companies).
            if (owned.CompanyId.HasValue && owned.CompanyId != _tenant.CompanyId)
                return null; // silently return null; controller maps this to 404
        }

        return entity;
    }

    // AsNoTracking: these are read-only operations; tracking adds memory overhead with no benefit.
    // GetAllAsync and FindAsync rely on global query filters from ApplicationDbContext for tenant isolation.
    //
    // Medium FIX — safety cap: GetAllAsync loads at most MaxRows rows.
    // If the table exceeds MaxRows rows this method throws InvalidOperationException instead
    // of silently truncating, which would cause payroll undercalculation (only the first
    // MaxRows employees would have payslips generated). Callers that hit the exception must
    // switch to GetPagedAsync() which correctly pages through the full dataset.
    public const int MaxRows = 500;

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        // Fetch one extra row so we can detect when the real count exceeds the cap.
        var items = await _set.AsNoTracking().Take(MaxRows + 1).ToListAsync();
        if (items.Count > MaxRows)
            throw new InvalidOperationException(
                $"GetAllAsync<{typeof(T).Name}>: result set exceeds the {MaxRows}-row safety cap. " +
                "Use GetPagedAsync() to page through large tables and avoid silent data truncation " +
                "that can cause payroll undercalculation.");
        return items;
    }

    // FIX HIGH-GR1: Paged read overload prevents full-table loads.
    // Page is 1-based; pageSize is capped at 500 to prevent accidental runaway queries.
    public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page     = Math.Max(1, page);
        var q     = _set.AsNoTracking();
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _set.AsNoTracking().Where(predicate).ToListAsync();

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) =>
        await _set.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(T entity) => await _set.AddAsync(entity);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);

    public async Task SaveChangesAsync() => await _ctx.SaveChangesAsync();
}
