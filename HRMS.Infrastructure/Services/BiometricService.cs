using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Lightweight biometric log service that queries EF directly from ApplicationDbContext.
/// Designed for unit tests that need a single-constructor dependency (just the DbContext)
/// without the full IBiometricDeviceService repository stack.
/// IBiometricDeviceService and BiometricDeviceService are left unchanged.
/// </summary>
public sealed class BiometricService : IBiometricService
{
    private readonly ApplicationDbContext _db;

    public BiometricService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<BiometricLogDto>> GetLogsAsync(
        int       companyId,
        string?   employeeId,
        DateTime? from,
        DateTime? to,
        int       page,
        int       pageSize,
        CancellationToken ct = default)
    {
        var query = _db.BiometricLogs
            .Where(l => l.CompanyId == companyId);

        if (employeeId is not null)
            query = query.Where(l => l.UserId == employeeId);

        if (from.HasValue)
            query = query.Where(l => l.PunchedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.PunchedAt <= to.Value);

        var total = await query.CountAsync(ct);

        var rawItems = await query
            .OrderByDescending(l => l.PunchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rawItems.Select(l => new BiometricLogDto(
            l.Id,
            l.BiometricDeviceId,
            l.Device?.Name ?? "-",
            l.UserId,
            l.CompanyId,
            l.PunchedAt,
            l.Direction.ToString(),
            l.DeviceSerial,
            l.IsProcessed,
            l.WebAttendanceId,
            l.SkipReason,
            l.CreatedAt)).ToList();

        return PagedResult<BiometricLogDto>.Create(items, total, page, pageSize);
    }
}
