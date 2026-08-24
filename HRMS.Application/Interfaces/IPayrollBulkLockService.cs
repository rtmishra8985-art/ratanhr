// FIX: Moved from HRMS.Infrastructure.Services to HRMS.Application.Interfaces.
// Interfaces belong in the Application layer (Clean Architecture boundary rule).
// Implementations (RedisPayrollBulkLockService, InMemoryPayrollBulkLockService)
// remain in HRMS.Infrastructure.Services and depend on this contract.

namespace HRMS.Application.Interfaces;

/// <summary>
/// Distributed mutual-exclusion lock for bulk payroll generation.
/// Ensures only one BulkGenerate request runs per (companyId, month, year) at a time.
/// </summary>
/// <remarks>
/// FIX HIGH-12: Prevents concurrent bulk payroll execution which could produce duplicate
/// or corrupted payslip records. Two implementations are registered based on Redis
/// availability: Redis-backed (distributed, multi-replica) and in-memory (single instance).
/// </remarks>
public interface IPayrollBulkLockService
{
    /// <summary>
    /// Attempts to acquire the payroll bulk-run lock for the given period.
    /// Returns a disposable token that releases the lock on dispose, or null if the lock is
    /// already held (caller should return HTTP 409 Conflict).
    /// </summary>
    Task<IPayrollBulkLockHandle?> TryAcquireAsync(
        int companyId, int month, int year, CancellationToken ct = default);
}

/// <summary>Returned by <see cref="IPayrollBulkLockService.TryAcquireAsync"/>. Dispose to release the lock.</summary>
public interface IPayrollBulkLockHandle : IAsyncDisposable { }
