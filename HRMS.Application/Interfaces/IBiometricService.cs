using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;

namespace HRMS.Application.Interfaces;

/// <summary>
/// Thin service interface used by unit tests for direct EF-backed biometric log queries.
/// Exposes flat parameters (no DTO filter wrapper) so tests can call without constructing
/// BiometricLogFilterDto. IBiometricDeviceService (the full orchestration interface) is
/// left unchanged.
/// </summary>
public interface IBiometricService
{
    Task<PagedResult<BiometricLogDto>> GetLogsAsync(
        int       companyId,
        string?   employeeId,
        DateTime? from,
        DateTime? to,
        int       page,
        int       pageSize,
        CancellationToken ct = default);
}
