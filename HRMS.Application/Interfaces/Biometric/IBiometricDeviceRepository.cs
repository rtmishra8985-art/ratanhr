using HRMS.Domain.Entities.Attendance;

namespace HRMS.Application.Interfaces.Biometric;

/// <summary>Repository for CRUD operations on BiometricDevice entities.</summary>
public interface IBiometricDeviceRepository
{
    Task<IReadOnlyList<BiometricDevice>> GetAllAsync(int companyId, CancellationToken ct = default);
    Task<BiometricDevice?> GetByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<BiometricDevice> AddAsync(BiometricDevice device, CancellationToken ct = default);
    Task UpdateAsync(BiometricDevice device, CancellationToken ct = default);
    Task DeleteAsync(int id, int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<BiometricDevice>> GetEnabledDevicesAsync(int companyId, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, Domain.Enums.BiometricStatus status, string? lastError, DateTime? lastPingAt, CancellationToken ct = default);
    Task UpdateLastSyncAsync(int id, DateTime syncAt, CancellationToken ct = default);
}
