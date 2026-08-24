using HRMS.Application.Common;
using HRMS.Application.Interfaces.Biometric;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Biometric;

/// <summary>EF Core implementation of IBiometricSyncHistoryRepository.</summary>
public sealed class BiometricSyncHistoryRepository : IBiometricSyncHistoryRepository
{
    private readonly ApplicationDbContext _db;
    public BiometricSyncHistoryRepository(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<BiometricSyncHistory>> GetPagedAsync(
        int companyId, int? deviceId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.BiometricSyncHistories
            .Where(h => h.CompanyId == companyId)
            .AsQueryable();

        if (deviceId.HasValue)
            q = q.Where(h => h.BiometricDeviceId == deviceId.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(h => h.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(h => h.Device)
            .AsNoTracking()
            .ToListAsync(ct);

        return PagedResult<BiometricSyncHistory>.Create(items, total, page, pageSize);
    }

    public async Task<BiometricSyncHistory> AddAsync(BiometricSyncHistory history, CancellationToken ct = default)
    {
        _db.BiometricSyncHistories.Add(history);
        await _db.SaveChangesAsync(ct);
        return history;
    }

    public async Task UpdateAsync(BiometricSyncHistory history, CancellationToken ct = default)
    {
        _db.BiometricSyncHistories.Update(history);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BiometricSyncHistory?> GetLatestAsync(int companyId, CancellationToken ct = default)
        => await _db.BiometricSyncHistories
            .Where(h => h.CompanyId == companyId && h.IsSuccess)
            .OrderByDescending(h => h.StartedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
}
