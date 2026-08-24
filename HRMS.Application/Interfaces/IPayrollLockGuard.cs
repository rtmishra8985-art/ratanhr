namespace HRMS.Application.Interfaces;

/// <summary>
/// Single shared guard for payroll-period lock enforcement.
/// Apply to: salary edits, attendance corrections, leave approval/cancellation,
/// payroll generation and reruns. No duplicated lock-check logic in callers.
/// </summary>
public interface IPayrollLockGuard
{
    /// <summary>
    /// Returns true when the specified company/month/year period is locked.
    /// Callers should treat a locked period as a business-rule block and return 409.
    /// </summary>
    Task<bool> IsLockedAsync(int companyId, int month, int year);

    /// <summary>
    /// Convenience helper: returns an error message when the period is locked,
    /// or null when it is open. Use at the top of every write-path handler.
    /// </summary>
    Task<string?> GetLockMessageAsync(int companyId, int month, int year);

    /// <summary>Lock the period for a company. Idempotent — re-locking is a no-op.</summary>
    Task LockAsync(int companyId, int month, int year, int lockedByUserId, string? notes = null);

    /// <summary>Unlock the period so corrections can be made.</summary>
    Task UnlockAsync(int companyId, int month, int year, int unlockedByUserId, string? notes = null);

    /// <summary>List all locks for a company (optionally filtered by year).</summary>
    Task<List<PayrollLockDto>> GetLocksAsync(int? companyId, int? year = null);
}

/// <summary>Read-only view of a payroll lock record.</summary>
public record PayrollLockDto(
    int      Id,
    int      CompanyId,
    int      Month,
    int      Year,
    bool     IsLocked,
    DateTime LockedAt,
    int      LockedByUserId,
    DateTime? UnlockedAt,
    int?      UnlockedByUserId,
    string?  Notes);
