using HRMS.Application.Interfaces.Biometric;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Orchestrates fetching punch logs from a biometric device and upserting
/// them as <see cref="WebAttendance"/> records in the HRMS database.
///
/// Multi-tenant scoping: WebAttendance is scoped to a company via the Employee
/// entity (Employee.CompanyId). Biometric device UserIds are expected to match
/// Employee.EmployeeId values. Unknown device users are silently skipped.
///
/// Strategy:
///   - Check-In / Unknown: create record if absent; take earliest punch if duplicate.
///   - Check-Out: update same day's record with the latest punch.
/// </summary>
public sealed class BiometricSyncService : IBiometricSyncService
{
    private readonly IBiometricProviderFactory     _factory;
    private readonly ApplicationDbContext          _db;
    private readonly ILogger<BiometricSyncService> _logger;

    public BiometricSyncService(
        IBiometricProviderFactory factory,
        ApplicationDbContext db,
        ILogger<BiometricSyncService> logger)
    {
        _factory = factory;
        _db      = db;
        _logger  = logger;
    }

    public async Task<int> SyncAttendanceAsync(
        string vendorName, int companyId,
        DateTime from, DateTime to,
        CancellationToken ct = default)
    {
        var provider = _factory.GetProvider(vendorName);
        _logger.LogInformation(
            "Biometric sync: vendor={Vendor} company={Company} {From:yyyy-MM-dd}–{To:yyyy-MM-dd}",
            vendorName, companyId, from, to);

        var logs = await provider.FetchLogsAsync(from, to, ct);
        if (logs.Count == 0)
        {
            _logger.LogInformation("Biometric sync: device returned 0 records.");
            return 0;
        }

        // Load the company's employee IDs for IDOR scoping.
        // WebAttendance has no CompanyId — isolation is enforced here via this set.
        var companyEmployeeIds = await _db.Employees
            .Where(e => e.CompanyId == companyId && e.IsActive)
            .Select(e => e.EmployeeCode)
            .ToListAsync(ct);
        var companyEmployeeIdSet = new HashSet<string>(companyEmployeeIds, StringComparer.Ordinal);

        var synced = 0;

        foreach (var log in logs)
        {
            // Skip records for employees not belonging to this company
            if (!companyEmployeeIdSet.Contains(log.UserId))
            {
                _logger.LogDebug(
                    "Biometric sync: skipping punch for unknown employee '{UserId}' (not in company {Company})",
                    log.UserId, companyId);
                continue;
            }

            var dateOnly = DateOnly.FromDateTime(log.PunchedAt);

            if (log.Direction is PunchDirection.CheckIn or PunchDirection.Unknown)
            {
                var existing = await _db.WebAttendances.FirstOrDefaultAsync(
                    a => a.EmployeeId == log.UserId && a.AttDate == dateOnly, ct);

                if (existing is null)
                {
                    _db.WebAttendances.Add(new WebAttendance
                    {
                        EmployeeId = log.UserId,
                        // BUGFIX: CompanyId was never set on biometric-synced attendance
                        // records. WebAttendance has a global EF query filter
                        // (!_filterByTenant || a.CompanyId == _tenantCompanyId) - a null
                        // CompanyId never matches a non-null _tenantCompanyId in SQL, so
                        // every record created by this sync path was invisible to company
                        // admins querying their own company's attendance (silently missing
                        // data, not an error). AttendanceService's web check-in path already
                        // sets CompanyId correctly; this path must match.
                        CompanyId  = companyId,
                        AttDate    = dateOnly,
                        CheckIn    = TimeOnly.FromDateTime(log.PunchedAt),
                        Status     = "Present",
                    });
                    synced++;
                }
                else if (existing.CheckIn is null || TimeOnly.FromDateTime(log.PunchedAt) < existing.CheckIn)
                {
                    existing.CheckIn = TimeOnly.FromDateTime(log.PunchedAt);
                }
            }
            else if (log.Direction is PunchDirection.CheckOut)
            {
                var existing = await _db.WebAttendances.FirstOrDefaultAsync(
                    a => a.EmployeeId == log.UserId && a.AttDate == dateOnly, ct);

                if (existing is not null &&
                    (existing.CheckOut is null || TimeOnly.FromDateTime(log.PunchedAt) > existing.CheckOut))
                {
                    existing.CheckOut = TimeOnly.FromDateTime(log.PunchedAt);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Biometric sync complete: {Count} new records created.", synced);
        return synced;
    }

    public Task<BiometricDeviceStatus> GetDeviceStatusAsync(
        string vendorName, CancellationToken ct = default)
        => _factory.GetProvider(vendorName).GetDeviceStatusAsync(ct);
}
