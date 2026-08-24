using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Single shared PayrollLockGuard.
/// Checks the <c>payroll_locks</c> table for an active lock on a company/month/year period.
/// Inject <see cref="IPayrollLockGuard"/> wherever a write-path operation must be blocked
/// during a closed payroll period.
/// </summary>
public sealed class PayrollLockGuard : IPayrollLockGuard
{
    private readonly ApplicationDbContext _db;

    public PayrollLockGuard(ApplicationDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<bool> IsLockedAsync(int companyId, int month, int year)
    {
        return await _db.PayrollLocks
            .AnyAsync(l => l.CompanyId == companyId
                        && l.Month == month
                        && l.Year == year
                        && l.IsLocked);
    }

    /// <inheritdoc/>
    public async Task<string?> GetLockMessageAsync(int companyId, int month, int year)
    {
        var locked = await IsLockedAsync(companyId, month, year);
        return locked
            ? $"Payroll period {month:00}/{year} for company {companyId} is locked. " +
              "Unlock the period before making changes."
            : null;
    }

    /// <inheritdoc/>
    public async Task LockAsync(int companyId, int month, int year, int lockedByUserId, string? notes = null)
    {
        var existing = await _db.PayrollLocks
            .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.Month == month && l.Year == year);

        if (existing is not null)
        {
            if (existing.IsLocked) return; // already locked — idempotent
            // Re-locking a previously unlocked period
            existing.IsLocked       = true;
            existing.LockedAt       = DateTime.UtcNow;
            existing.LockedByUserId = lockedByUserId;
            existing.UnlockedAt     = null;
            existing.UnlockedByUserId = null;
            existing.Notes          = notes;
        }
        else
        {
            _db.PayrollLocks.Add(new PayrollLock
            {
                CompanyId       = companyId,
                Month           = month,
                Year            = year,
                IsLocked        = true,
                LockedAt        = DateTime.UtcNow,
                LockedByUserId  = lockedByUserId,
                Notes           = notes
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UnlockAsync(int companyId, int month, int year, int unlockedByUserId, string? notes = null)
    {
        var existing = await _db.PayrollLocks
            .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.Month == month && l.Year == year);

        if (existing is null || !existing.IsLocked) return; // not locked — no-op

        existing.IsLocked         = false;
        existing.UnlockedAt       = DateTime.UtcNow;
        existing.UnlockedByUserId = unlockedByUserId;
        existing.Notes            = notes ?? existing.Notes;

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<List<PayrollLockDto>> GetLocksAsync(int? companyId, int? year = null)
    {
        // FIX AUDIT-07S-03: PayrollLock.CompanyId is a non-nullable int while the
        // parameter is int? (null == SuperAdmin / unrestricted). The previous
        // `l.CompanyId == companyId` comparison lifted to a nullable compare, so a null
        // argument translated to `company_id = NULL`, which is never true in SQL and
        // silently returned an EMPTY lock list to SuperAdmin. Scope the filter only when
        // a concrete company is supplied.
        var query = _db.PayrollLocks.AsQueryable();
        if (companyId.HasValue)
        {
            var cid = companyId.Value;
            query = query.Where(l => l.CompanyId == cid);
        }
        if (year.HasValue) query = query.Where(l => l.Year == year.Value);

        return await query
            .OrderByDescending(l => l.Year).ThenByDescending(l => l.Month)
            .Select(l => new PayrollLockDto(
                l.Id, l.CompanyId, l.Month, l.Year,
                l.IsLocked, l.LockedAt, l.LockedByUserId,
                l.UnlockedAt, l.UnlockedByUserId, l.Notes))
            .ToListAsync();
    }
}
