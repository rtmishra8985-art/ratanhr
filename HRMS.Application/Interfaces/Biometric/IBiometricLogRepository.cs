using HRMS.Domain.Entities.Attendance;
using HRMS.Application.Common;

namespace HRMS.Application.Interfaces.Biometric;

/// <summary>Repository for reading and writing BiometricLog (raw punch) records.</summary>
public interface IBiometricLogRepository
{
    Task<PagedResult<BiometricLog>> GetPagedAsync(
        int      companyId,
        int?     deviceId,
        string?  userId,
        DateTime? from,
        DateTime? to,
        bool?    isProcessed,
        int      page,
        int      pageSize,
        CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<BiometricLog> logs, CancellationToken ct = default);

    Task MarkProcessedAsync(int logId, int webAttendanceId, CancellationToken ct = default);

    /// <summary>Delete logs older than the specified cutoff for all companies (used by cleanup service).</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);

    Task<int> CountTodayAsync(int companyId, CancellationToken ct = default);
    Task<int> CountUnprocessedAsync(int companyId, CancellationToken ct = default);
}
