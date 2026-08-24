using HRMS.Application.Common;
using HRMS.Application.DTOs.Holiday;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Holiday calendar service with cache-aside (Fix 5).
/// Holiday lists are looked up on every attendance/leave calculation
/// but change infrequently. A 10-minute TTL keeps the cache fresh
/// without hammering the database on every request.
/// </summary>
public class HolidayService : IHolidayService
{
    private readonly ApplicationDbContext _db;
    private readonly ICacheService _cache;

    private static string ListKey(int? companyId, int? year) => $"holiday:list:{companyId}:{year}";
    // FIX [CACHE-IDOR]: Key must include caller scope so a company-A user cannot
    // warm the cache with their visibility check result and have company-B receive it.
    // SuperAdmin (callerCompanyId == null) gets its own "sa" partition.
    private static string ByIdKey(int id, int? callerCompanyId) =>
        $"holiday:id:{id}:c{callerCompanyId?.ToString() ?? "sa"}";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public HolidayService(ApplicationDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public Task<List<HolidayDto>> GetAllAsync(int? companyId, int? year) =>
        _cache.GetOrSetAsync(ListKey(companyId, year), async () =>
        {
            var q = _db.HolidayCalendars
                .Where(h => h.IsActive && (h.CompanyId == null || h.CompanyId == companyId))
                .AsNoTracking();
            if (year.HasValue)
                q = q.Where(h => h.Date.Year == year.Value);
            var list = await q.OrderBy(h => h.Date).ToListAsync();
            return list.Select(Map).ToList();
        }, Ttl);

    public async Task<PagedResult<HolidayDto>> GetAllPagedAsync(
        int? companyId,
        int? year,
        int page,
        int pageSize,
        string? search = null,
        bool? isOptional = null,
        string? sortBy = null,
        string? sortDirection = "asc")
    {
        // Paged queries are not cached — key-space would explode.
        var q = _db.HolidayCalendars
            .Where(h => h.IsActive && (h.CompanyId == null || h.CompanyId == companyId))
            .AsNoTracking();
        if (year.HasValue) q = q.Where(h => h.Date.Year == year.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(h =>
                h.Name.ToLower().Contains(term) ||
                (h.Description != null && h.Description.ToLower().Contains(term)));
        }
        if (isOptional.HasValue)
            q = q.Where(h => h.IsOptional == isOptional.Value);

        var allowed = new[] { "Name", "Date", "CreatedAt", "IsOptional" };
        q = q.ApplySorting(sortBy, sortDirection, h => h.Date, allowed);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<HolidayDto>.Create(rows.Select(Map).ToList(), total, page, pageSize);
    }

    // FIX [2] IDOR — enforce company ownership on single-record reads.
    // callerCompanyId == null means SuperAdmin (unrestricted scope).
    // Global holidays (CompanyId == null) are visible to every authenticated user.
    public Task<HolidayDto?> GetByIdAsync(int id, int? callerCompanyId) =>
        _cache.GetOrSetAsync(ByIdKey(id, callerCompanyId), async () =>
        {
            var h = await _db.HolidayCalendars.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (h == null) return null;
            // Global holiday: visible to all.
            // Company-specific: visible only to that company's users or SuperAdmin.
            if (h.CompanyId != null && callerCompanyId != null && h.CompanyId != callerCompanyId)
                return null;
            return Map(h);
        }, Ttl);

    public async Task<HolidayDto> CreateAsync(int? companyId, CreateHolidayDto dto)
    {
        if (!DateOnly.TryParse(dto.Date, out var date))
            throw new ArgumentException("Invalid date format. Use yyyy-MM-dd.");

        var h = new HolidayCalendar
        {
            CompanyId   = companyId,
            Name        = dto.Name.Trim(),
            Date        = date,
            Description = dto.Description?.Trim(),
            IsOptional  = dto.IsOptional,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
        _db.HolidayCalendars.Add(h);
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("holiday:");
        return Map(h);
    }

    // FIX [2] IDOR — enforce company ownership before mutating.
    // callerCompanyId == null means SuperAdmin (unrestricted scope).
    // isSuperAdmin distinguishes a SuperAdmin (null because privileged) from a
    // misconfigured token (null because claim missing). Global holidays
    // (CompanyId == null) may only be modified by SuperAdmin.
    public async Task<bool> UpdateAsync(int id, CreateHolidayDto dto, int? callerCompanyId, bool isSuperAdmin)
    {
        // FIX IDOR: FirstOrDefaultAsync respects EF Core global query filters that
        // FindAsync bypasses. Ownership enforcement continues via the guards below.
        var h = await _db.HolidayCalendars.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return false;

        if (h.CompanyId == null)
        {
            // Global record — SuperAdmin only.
            if (!isSuperAdmin) return false;
        }
        else
        {
            // Company-specific record — must match caller's company (SuperAdmin bypasses).
            if (!isSuperAdmin && callerCompanyId != null && h.CompanyId != callerCompanyId)
                return false;
        }

        if (!DateOnly.TryParse(dto.Date, out var date))
            throw new ArgumentException("Invalid date format. Use yyyy-MM-dd.");
        h.Name        = dto.Name.Trim();
        h.Date        = date;
        h.Description = dto.Description?.Trim();
        h.IsOptional  = dto.IsOptional;
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("holiday:");
        return true;
    }

    // FIX [2] IDOR — enforce company ownership before deleting.
    public async Task<bool> DeleteAsync(int id, int? callerCompanyId, bool isSuperAdmin)
    {
        // FIX IDOR: FirstOrDefaultAsync respects EF Core global query filters.
        var h = await _db.HolidayCalendars.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return false;

        if (h.CompanyId == null)
        {
            // Global record — SuperAdmin only.
            if (!isSuperAdmin) return false;
        }
        else
        {
            if (!isSuperAdmin && callerCompanyId != null && h.CompanyId != callerCompanyId)
                return false;
        }

        h.IsActive = false;
        await _db.SaveChangesAsync();
        await _cache.RemoveByPrefixAsync("holiday:");
        return true;
    }

    private static HolidayDto Map(HolidayCalendar h) => new()
    {
        Id          = h.Id,
        CompanyId   = h.CompanyId,
        Name        = h.Name,
        Date        = h.Date.ToString("yyyy-MM-dd"),
        Description = h.Description,
        IsOptional  = h.IsOptional,
        IsActive    = h.IsActive,
        CreatedAt   = h.CreatedAt
    };
}
