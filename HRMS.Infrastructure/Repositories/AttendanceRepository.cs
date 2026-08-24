using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories;

public interface IAttendanceRepository
{
    Task<List<WebAttendance>> GetWebAttendanceAsync(string? employeeId, int? companyId, int? month, int? year);
    Task<WebAttendance?> GetTodayWebAttendanceAsync(string employeeId);
    Task<List<ExcelAttendance>> GetExcelAttendanceAsync(string? employeeId, int? companyId, int? month, int? year);
    Task<List<Shift>> GetShiftsAsync(int companyId);
    Task<Shift?> GetShiftByIdAsync(int shiftId);
    Task AddShiftAsync(Shift shift);
    Task SaveChangesAsync();
}

public class AttendanceRepository : IAttendanceRepository
{
    private readonly ApplicationDbContext _ctx;
    public AttendanceRepository(ApplicationDbContext ctx) => _ctx = ctx;

    // FIX HIGH-AR1: Added Take(500) cap to prevent loading an unbounded number of
    // attendance records into memory when no date/employee filter is applied.
    // Callers that need a full export should use the streaming report path instead.
    private const int MaxRecordsPerQuery = 500;

    public async Task<List<WebAttendance>> GetWebAttendanceAsync(string? employeeId, int? companyId, int? month, int? year)
    {
        var q = _ctx.WebAttendances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(a => a.EmployeeId == employeeId);
        if (companyId.HasValue) q = q.Where(a => a.CompanyId == companyId);
        if (month.HasValue) q = q.Where(a => a.AttDate.Month == month);
        if (year.HasValue) q = q.Where(a => a.AttDate.Year == year);
        return await q.OrderByDescending(a => a.AttDate).Take(MaxRecordsPerQuery).ToListAsync();
    }

    public async Task<WebAttendance?> GetTodayWebAttendanceAsync(string employeeId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _ctx.WebAttendances.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttDate == today);
    }

    public async Task<List<ExcelAttendance>> GetExcelAttendanceAsync(string? employeeId, int? companyId, int? month, int? year)
    {
        var q = _ctx.ExcelAttendances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(a => a.EmployeeId == employeeId);
        if (companyId.HasValue) q = q.Where(a => a.CompanyId == companyId);
        if (month.HasValue) q = q.Where(a => a.AttDate.Month == month);
        if (year.HasValue) q = q.Where(a => a.AttDate.Year == year);
        // FIX HIGH-AR2: Cap result set; streaming export path should be used for > 500 records.
        return await q.OrderByDescending(a => a.AttDate).Take(MaxRecordsPerQuery).ToListAsync();
    }

    public async Task<List<Shift>> GetShiftsAsync(int companyId)
        => await _ctx.Shifts.Where(s => s.CompanyId == companyId && s.IsActive).ToListAsync();

    public async Task<Shift?> GetShiftByIdAsync(int shiftId) => await _ctx.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId);

    public async Task AddShiftAsync(Shift shift) => await _ctx.Shifts.AddAsync(shift);

    public async Task SaveChangesAsync() => await _ctx.SaveChangesAsync();
}
