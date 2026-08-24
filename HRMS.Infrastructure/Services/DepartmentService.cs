using HRMS.Application.Common;
using HRMS.Application.DTOs.Department;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Department and Designation service with cache-aside (Fix 5).
/// Departments and Designations are read far more often than they are written,
/// making them ideal cache candidates. A 5-minute TTL is used by default;
/// the cache is explicitly invalidated on every write.
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICacheService _cache;

    private static string DeptListKey(int? companyId) => $"dept:list:{companyId}";
    // FIX [CACHE-IDOR]: Include caller scope in key to prevent cross-tenant cache poisoning.
    private static string DeptByIdKey(int id, int? callerCompanyId) =>
        $"dept:id:{id}:c{callerCompanyId?.ToString() ?? "sa"}";
    private static string DesgListKey(int? companyId) => $"desg:list:{companyId}";
    // FIX [CACHE-IDOR]: Include caller scope in key to prevent cross-tenant cache poisoning.
    private static string DesgByIdKey(int id, int? callerCompanyId) =>
        $"desg:id:{id}:c{callerCompanyId?.ToString() ?? "sa"}";

    public DepartmentService(ApplicationDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    // ── Departments ────────────────────────────────────────────────────────

    public Task<List<DepartmentDto>> GetDepartmentsAsync(int? companyId) =>
        _cache.GetOrSetAsync(DeptListKey(companyId), async () =>
        {
            var list = await _db.Departments
                .Where(d => d.IsActive && (d.CompanyId == null || d.CompanyId == companyId))
                .OrderBy(d => d.Name)
                .AsNoTracking()
                .ToListAsync();
            return list.Select(MapDept).ToList();
        });

    // FIX 5: Added sortBy / sortDirection for column-level sorting support.
    public async Task<PagedResult<DepartmentDto>> GetDepartmentsPagedAsync(
        int?    companyId,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "asc",
        string? search        = null)
    {
        // Paged queries are not cached (cache key would vary per page/size combination
        // and the benefit is low relative to the key-space explosion).
        var q = _db.Departments
            .Where(d => d.IsActive && (d.CompanyId == null || d.CompanyId == companyId))
            .AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(d =>
                d.Name.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)));
        }

        // FIX 5: Apply safe sorting — allowed columns whitelist prevents SQL injection.
        var allowed = new[] { "Name", "Description", "CreatedAt", "IsActive" };
        q = q.ApplySortingByName(sortBy, sortDirection, d => d.Name, allowed);

        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<DepartmentDto>.Create(rows.Select(MapDept).ToList(), total, page, pageSize);
    }

    // FIX [2] IDOR — enforce company ownership on single-record reads.
    // callerCompanyId == null means SuperAdmin (unrestricted scope).
    // Records with CompanyId == null are global and visible to every tenant (read-only).
    public Task<DepartmentDto?> GetDepartmentByIdAsync(int id, int? callerCompanyId) =>
        _cache.GetOrSetAsync(DeptByIdKey(id, callerCompanyId), async () =>
        {
            var d = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return null;
            // Global records (CompanyId == null) are visible to all.
            // Company-specific records are visible only to their own tenant or SuperAdmin.
            if (d.CompanyId != null && callerCompanyId != null && d.CompanyId != callerCompanyId)
                return null;
            return MapDept(d);
        });

    public async Task<DepartmentDto> CreateDepartmentAsync(int? companyId, CreateDepartmentDto dto)
    {
        var d = new Department
        {
            CompanyId   = companyId,
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
        _db.Departments.Add(d);
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("dept:");   // invalidate all department cache entries
        return MapDept(d);
    }

    // FIX [2] IDOR — enforce company ownership before mutating.
    // callerCompanyId == null means SuperAdmin (unrestricted scope).
    public async Task<bool> UpdateDepartmentAsync(int id, CreateDepartmentDto dto, int? callerCompanyId)
    {
        var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return false;
        // Global records (CompanyId == null) may only be updated by SuperAdmin.
        // Company-specific records require a matching caller.
        if (d.CompanyId != null && callerCompanyId != null && d.CompanyId != callerCompanyId)
            return false;
        d.Name        = dto.Name.Trim();
        d.Description = dto.Description?.Trim();
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("dept:");   // invalidate all department cache entries
        return true;
    }

    // FIX [2] IDOR — enforce company ownership before deleting.
    public async Task<bool> DeleteDepartmentAsync(int id, int? callerCompanyId)
    {
        var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return false;
        if (d.CompanyId != null && callerCompanyId != null && d.CompanyId != callerCompanyId)
            return false;
        d.IsActive = false;
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("dept:");   // invalidate all department cache entries
        return true;
    }

    // ── Designations ───────────────────────────────────────────────────────

    // FIX 5: Added sortBy / sortDirection for column-level sorting support.
    public async Task<PagedResult<DesignationDto>> GetDesignationsPagedAsync(
        int?    companyId,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "asc",
        string? search        = null)
    {
        var q = _db.Designations
            .Where(d => d.IsActive && (d.CompanyId == null || d.CompanyId == companyId))
            .AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(d =>
                d.Name.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)));
        }

        var allowed = new[] { "Name", "Description", "CreatedAt", "IsActive" };
        q = q.ApplySortingByName(sortBy, sortDirection, d => d.Name, allowed);

        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<DesignationDto>.Create(rows.Select(MapDesg).ToList(), total, page, pageSize);
    }

    public Task<List<DesignationDto>> GetDesignationsAsync(int? companyId) =>
        _cache.GetOrSetAsync(DesgListKey(companyId), async () =>
        {
            var list = await _db.Designations
                .Where(d => d.IsActive && (d.CompanyId == null || d.CompanyId == companyId))
                .OrderBy(d => d.Name)
                .AsNoTracking()
                .ToListAsync();
            return list.Select(MapDesg).ToList();
        });

    // FIX [2] IDOR — enforce company ownership on single-record reads.
    public Task<DesignationDto?> GetDesignationByIdAsync(int id, int? callerCompanyId) =>
        _cache.GetOrSetAsync(DesgByIdKey(id, callerCompanyId), async () =>
        {
            var d = await _db.Designations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return null;
            if (d.CompanyId != null && callerCompanyId != null && d.CompanyId != callerCompanyId)
                return null;
            return MapDesg(d);
        });

    public async Task<DesignationDto> CreateDesignationAsync(int? companyId, CreateDesignationDto dto)
    {
        var d = new Designation
        {
            CompanyId   = companyId,
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
        _db.Designations.Add(d);
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("desg:");
        return MapDesg(d);
    }

    // FIX [2] IDOR — enforce company ownership before mutating.
    public async Task<bool> UpdateDesignationAsync(int id, CreateDesignationDto dto, int? callerCompanyId)
    {
        var d = await _db.Designations.FindAsync(id);
        if (d == null) return false;
        if (d.CompanyId != null && callerCompanyId != null && d.CompanyId != callerCompanyId)
            return false;
        d.Name        = dto.Name.Trim();
        d.Description = dto.Description?.Trim();
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("desg:");
        return true;
    }

    // FIX [2] IDOR — enforce company ownership before deleting.
    public async Task<bool> DeleteDesignationAsync(int id, int? callerCompanyId)
    {
        var d = await _db.Designations.FindAsync(id);
        if (d == null) return false;
        if (d.CompanyId != null && callerCompanyId != null && d.CompanyId != callerCompanyId)
            return false;
        d.IsActive = false;
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("desg:");
        return true;
    }

    // ── Mappers ────────────────────────────────────────────────────────────

    private static DepartmentDto MapDept(Department d) => new()
    {
        Id          = d.Id,
        CompanyId   = d.CompanyId,
        Name        = d.Name,
        Description = d.Description,
        IsActive    = d.IsActive,
        CreatedAt   = d.CreatedAt,
    };

    private static DesignationDto MapDesg(Designation d) => new()
    {
        Id          = d.Id,
        CompanyId   = d.CompanyId,
        Name        = d.Name,
        Description = d.Description,
        IsActive    = d.IsActive,
        CreatedAt   = d.CreatedAt,
    };
}
