using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Company;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HRMS.Infrastructure.Services;

// FIX LOW: Inject IMemoryCache to cache CompanySettings (read on nearly every payroll/attendance
// request but rarely mutated). Cache is invalidated on UpsertSettingsAsync.
public class CompanySettingsService : ICompanySettingsService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IMemoryCache _cache;
    private static string CacheKey(int companyId) => $"company_settings_{companyId}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public CompanySettingsService(ApplicationDbContext ctx, IMemoryCache cache)
    {
        _ctx   = ctx;
        _cache = cache;
    }

    public async Task<CompanySettingsDto?> GetSettingsAsync(int companyId)
    {
        if (_cache.TryGetValue(CacheKey(companyId), out CompanySettingsDto? cached))
            return cached;

        var s = await _ctx.CompanySettings.FirstOrDefaultAsync(x => x.CompanyId == companyId);
        var dto = s == null
            ? new CompanySettingsDto { CompanyId = companyId, WorkingDaysPerMonth = 26,
                PFPercentage = 12, ESIPercentage = 0.75m, PTAmount = 200 }
            : new CompanySettingsDto { Id = s.Id, CompanyId = s.CompanyId,
                WorkingDaysPerMonth = s.WorkingDaysPerMonth, PFPercentage = s.PFPercentage,
                ESIPercentage = s.ESIPercentage, PTAmount = s.PTAmount,
                PayslipFooterNote = s.PayslipFooterNote, TimeZone = s.TimeZone,
                CheckInTime = s.CheckInTime?.ToString("HH:mm"),
                CheckOutTime = s.CheckOutTime?.ToString("HH:mm"),
                OvertimeThresholdMinutes = s.OvertimeThresholdMinutes };

        _cache.Set(CacheKey(companyId), dto, CacheTtl);
        return dto;
    }

    public async Task UpsertSettingsAsync(UpsertCompanySettingsDto dto)
    {
        var s = await _ctx.CompanySettings.FirstOrDefaultAsync(x => x.CompanyId == dto.CompanyId);
        if (s == null) {
            s = new CompanySettings { CompanyId = dto.CompanyId, CreatedAt = DateTime.UtcNow };
            _ctx.CompanySettings.Add(s);
        }
        s.WorkingDaysPerMonth = dto.WorkingDaysPerMonth; s.PFPercentage = dto.PFPercentage;
        s.ESIPercentage = dto.ESIPercentage; s.PTAmount = dto.PTAmount;
        s.PayslipFooterNote = dto.PayslipFooterNote; s.TimeZone = dto.TimeZone;
        if (!string.IsNullOrEmpty(dto.CheckInTime) && TimeOnly.TryParse(dto.CheckInTime, out var ci)) s.CheckInTime = ci;
        if (!string.IsNullOrEmpty(dto.CheckOutTime) && TimeOnly.TryParse(dto.CheckOutTime, out var co)) s.CheckOutTime = co;
        s.OvertimeThresholdMinutes = dto.OvertimeThresholdMinutes; s.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();

        // Invalidate cache after mutation so next read gets fresh data.
        _cache.Remove(CacheKey(dto.CompanyId));
    }
}
