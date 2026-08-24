using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Provides upsert-style access to per-company BiometricSettings.
/// If no settings row exists for a company, a default one is created on first read.
/// </summary>
public sealed class BiometricSettingsRepository
{
    private readonly ApplicationDbContext _db;
    public BiometricSettingsRepository(ApplicationDbContext db) => _db = db;

    public async Task<BiometricSettings> GetOrCreateAsync(int companyId, CancellationToken ct = default)
    {
        var settings = await _db.BiometricSettings
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        if (settings is not null) return settings;

        settings = new BiometricSettings { CompanyId = companyId };
        _db.BiometricSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task UpdateAsync(BiometricSettings settings, CancellationToken ct = default)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _db.BiometricSettings.Update(settings);
        await _db.SaveChangesAsync(ct);
    }
}
