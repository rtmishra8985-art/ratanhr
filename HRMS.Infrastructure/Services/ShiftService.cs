using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class ShiftService : IShiftService
{
    private readonly ApplicationDbContext _ctx;
    public ShiftService(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<List<ShiftDto>> GetShiftsAsync(int companyId)
    {
        // Materialize first — EF Core cannot translate a static mapper method into SQL.
        var rows = await _ctx.Shifts
            .Where(s => s.CompanyId == companyId && s.IsActive)
            .ToListAsync();
        return rows.Select(MapDto).ToList();
    }

    public async Task<int> CreateShiftAsync(CreateShiftDto dto)
    {
        var s = new Shift { CompanyId = dto.CompanyId, ShiftName = dto.ShiftName, IsNightShift = dto.IsNightShift,
            GracePeriodMinutes = dto.GracePeriodMinutes, CreatedAt = DateTime.UtcNow };
        if (TimeOnly.TryParse(dto.StartTime, out var st)) s.StartTime = st;
        if (TimeOnly.TryParse(dto.EndTime, out var et)) s.EndTime = et;
        _ctx.Shifts.Add(s);
        await _ctx.SaveChangesAsync();
        return s.Id;
    }

    // FIX [2] IDOR — verify Shift.CompanyId == callerCompanyId before mutating.
    // callerCompanyId == null means the caller is a SuperAdmin (unrestricted scope).
    public async Task<bool> UpdateShiftAsync(int id, CreateShiftDto dto, int? callerCompanyId)
    {
        // FIX IDOR: replace two-step FindAsync + secondary guard with a single
        // company-scoped query. FindAsync bypasses EF Core global query filters;
        // FirstOrDefaultAsync respects them. SuperAdmin (null) → unrestricted.
        var s = callerCompanyId.HasValue
            ? await _ctx.Shifts.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == callerCompanyId)
            : await _ctx.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return false;
        s.ShiftName = dto.ShiftName; s.IsNightShift = dto.IsNightShift; s.GracePeriodMinutes = dto.GracePeriodMinutes;
        if (TimeOnly.TryParse(dto.StartTime, out var st)) s.StartTime = st;
        if (TimeOnly.TryParse(dto.EndTime, out var et)) s.EndTime = et;
        await _ctx.SaveChangesAsync();
        return true;
    }

    // FIX [2] IDOR — verify Shift.CompanyId == callerCompanyId before deleting.
    // callerCompanyId == null means the caller is a SuperAdmin (unrestricted scope).
    public async Task<bool> DeleteShiftAsync(int id, int? callerCompanyId)
    {
        // FIX IDOR: single company-scoped query replaces FindAsync + secondary check.
        var s = callerCompanyId.HasValue
            ? await _ctx.Shifts.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == callerCompanyId)
            : await _ctx.Shifts.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return false;
        s.IsActive = false;   // soft-delete — preserves shift assignment history
        await _ctx.SaveChangesAsync();
        return true;
    }

    private static ShiftDto MapDto(Shift s) => new() { Id = s.Id, CompanyId = s.CompanyId,
        ShiftName = s.ShiftName, StartTime = s.StartTime.ToString("HH:mm"),
        EndTime = s.EndTime.ToString("HH:mm"), GracePeriodMinutes = s.GracePeriodMinutes,
        IsNightShift = s.IsNightShift, IsActive = s.IsActive, CreatedAt = s.CreatedAt };

    // BUG FIX: previously took non-nullable `int companyId`, so the controller (which
    // passes -1 for SuperAdmin with no override, per BaseController.CompanyId's sentinel)
    // always filtered on the impossible company_id = -1 and returned an empty page instead
    // of the "unrestricted cross-tenant view" the controller's own comments describe.
    // null now means unrestricted, matching every other paged tenant-scoped service.
    public async Task<PagedResult<ShiftDto>> GetShiftsPagedAsync(int? companyId, int page, int pageSize)
        => await _ctx.Shifts
            .Where(s => !companyId.HasValue || s.CompanyId == companyId)
            .Where(s => s.IsActive)
            .OrderBy(s => s.ShiftName)
            .Select(s => new ShiftDto { Id = s.Id, CompanyId = s.CompanyId, ShiftName = s.ShiftName,
                StartTime = s.StartTime.ToString("HH:mm"), EndTime = s.EndTime.ToString("HH:mm"),
                GracePeriodMinutes = s.GracePeriodMinutes, IsNightShift = s.IsNightShift,
                IsActive = s.IsActive, CreatedAt = s.CreatedAt })
            .ToPagedResultAsync(page, pageSize);
}
