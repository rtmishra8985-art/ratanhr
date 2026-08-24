using HRMS.Application.Interfaces.Biometric;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Enums;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Biometric;

/// <summary>EF Core implementation of IBiometricDeviceRepository.</summary>
public sealed class BiometricDeviceRepository : IBiometricDeviceRepository
{
    private readonly ApplicationDbContext _db;
    public BiometricDeviceRepository(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BiometricDevice>> GetAllAsync(int companyId, CancellationToken ct = default)
        => await _db.BiometricDevices
            .Where(d => d.CompanyId == companyId)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<BiometricDevice?> GetByIdAsync(int id, int companyId, CancellationToken ct = default)
        => await _db.BiometricDevices
            .FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == companyId, ct);

    public async Task<BiometricDevice> AddAsync(BiometricDevice device, CancellationToken ct = default)
    {
        _db.BiometricDevices.Add(device);
        await _db.SaveChangesAsync(ct);
        return device;
    }

    public async Task UpdateAsync(BiometricDevice device, CancellationToken ct = default)
    {
        device.UpdatedAt = DateTime.UtcNow;
        _db.BiometricDevices.Update(device);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, int companyId, CancellationToken ct = default)
    {
        var device = await GetByIdAsync(id, companyId, ct)
            ?? throw new KeyNotFoundException($"BiometricDevice {id} not found.");
        _db.BiometricDevices.Remove(device);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BiometricDevice>> GetEnabledDevicesAsync(int companyId, CancellationToken ct = default)
        => await _db.BiometricDevices
            .Where(d => d.CompanyId == companyId && d.IsEnabled && d.Status != BiometricStatus.Disabled)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task UpdateStatusAsync(int id, BiometricStatus status, string? lastError, DateTime? lastPingAt, CancellationToken ct = default)
    {
        await _db.BiometricDevices
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status,    status)
                .SetProperty(d => d.LastError,  lastError)
                .SetProperty(d => d.LastPingAt, lastPingAt)
                .SetProperty(d => d.UpdatedAt,  DateTime.UtcNow), ct);
    }

    public async Task UpdateLastSyncAsync(int id, DateTime syncAt, CancellationToken ct = default)
    {
        await _db.BiometricDevices
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastSyncAt, syncAt)
                .SetProperty(d => d.UpdatedAt,  DateTime.UtcNow), ct);
    }
}
