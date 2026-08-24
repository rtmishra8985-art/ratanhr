using HRMS.Domain.Entities.Attendance;
using HRMS.Application.Common;

namespace HRMS.Application.Interfaces.Biometric;

/// <summary>Repository for reading and writing BiometricSyncHistory audit records.</summary>
public interface IBiometricSyncHistoryRepository
{
    Task<PagedResult<BiometricSyncHistory>> GetPagedAsync(
        int  companyId,
        int? deviceId,
        int  page,
        int  pageSize,
        CancellationToken ct = default);

    Task<BiometricSyncHistory> AddAsync(BiometricSyncHistory history, CancellationToken ct = default);
    Task UpdateAsync(BiometricSyncHistory history, CancellationToken ct = default);
    Task<BiometricSyncHistory?> GetLatestAsync(int companyId, CancellationToken ct = default);
}
