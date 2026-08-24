using HRMS.Application.Common;
using HRMS.Application.Interfaces.Biometric;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Biometric;

/// <summary>EF Core implementation of IBiometricLogRepository.</summary>
public sealed class BiometricLogRepository : IBiometricLogRepository
{
    private readonly ApplicationDbContext _db;
    public BiometricLogRepository(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<BiometricLog>> GetPagedAsync(
        int companyId, int? deviceId, string? userId,
        DateTime? from, DateTime? to, bool? isProcessed,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.BiometricLogs
            .Where(l => l.CompanyId == companyId)
            .AsQueryable();

        if (deviceId.HasValue)   q = q.Where(l => l.BiometricDeviceId == deviceId.Value);
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(l => l.UserId == userId);
        if (from.HasValue)        q = q.Where(l => l.PunchedAt >= from.Value);
        if (to.HasValue)          q = q.Where(l => l.PunchedAt <= to.Value);
        if (isProcessed.HasValue) q = q.Where(l => l.IsProcessed == isProcessed.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.PunchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(l => l.Device)
            .AsNoTracking()
            .ToListAsync(ct);

        return PagedResult<BiometricLog>.Create(items, total, page, pageSize);
    }

    public async Task AddRangeAsync(IEnumerable<BiometricLog> logs, CancellationToken ct = default)
    {
        await _db.BiometricLogs.AddRangeAsync(logs, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkProcessedAsync(int logId, int webAttendanceId, CancellationToken ct = default)
    {
        await _db.BiometricLogs
            .Where(l => l.Id == logId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.IsProcessed,     true)
                .SetProperty(l => l.WebAttendanceId, webAttendanceId), ct);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
        => await _db.BiometricLogs
            .Where(l => l.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

    public async Task<int> CountTodayAsync(int companyId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.BiometricLogs
            .CountAsync(l => l.CompanyId == companyId && l.CreatedAt >= today, ct);
    }

    public async Task<int> CountUnprocessedAsync(int companyId, CancellationToken ct = default)
        => await _db.BiometricLogs
            .CountAsync(l => l.CompanyId == companyId && !l.IsProcessed, ct);
}
