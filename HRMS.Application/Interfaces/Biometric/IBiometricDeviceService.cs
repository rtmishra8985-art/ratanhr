using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Common;

namespace HRMS.Application.Interfaces.Biometric;

/// <summary>
/// Application service for biometric device management and related operations.
/// Orchestrates device CRUD, connectivity testing, sync history, settings, and dashboard data.
/// </summary>
public interface IBiometricDeviceService
{
    // ── Device CRUD ───────────────────────────────────────────────────────
    Task<IReadOnlyList<BiometricDeviceDto>> GetDevicesAsync(int companyId, CancellationToken ct = default);
    Task<BiometricDeviceDto?> GetDeviceByIdAsync(int id, int companyId, CancellationToken ct = default);
    Task<BiometricDeviceDto> CreateDeviceAsync(int companyId, CreateBiometricDeviceDto dto, CancellationToken ct = default);
    Task<BiometricDeviceDto> UpdateDeviceAsync(int id, int companyId, UpdateBiometricDeviceDto dto, CancellationToken ct = default);
    Task DeleteDeviceAsync(int id, int companyId, CancellationToken ct = default);

    // ── Device Control ────────────────────────────────────────────────────
    Task EnableDeviceAsync(int id, int companyId, CancellationToken ct = default);
    Task DisableDeviceAsync(int id, int companyId, CancellationToken ct = default);

    /// <summary>Ping the device and update its status in the DB. Returns current status snapshot.</summary>
    Task<BiometricDeviceStatusDto> TestConnectionAsync(int id, int companyId, CancellationToken ct = default);

    // ── Logs ──────────────────────────────────────────────────────────────
    Task<PagedResult<BiometricLogDto>> GetLogsAsync(int companyId, BiometricLogFilterDto filter, CancellationToken ct = default);

    // ── Sync History ──────────────────────────────────────────────────────
    Task<PagedResult<BiometricSyncHistoryDto>> GetSyncHistoryAsync(int companyId, int? deviceId, int page, int pageSize, CancellationToken ct = default);

    // ── Settings ──────────────────────────────────────────────────────────
    Task<BiometricSettingsDto> GetSettingsAsync(int companyId, CancellationToken ct = default);
    Task<BiometricSettingsDto> UpdateSettingsAsync(int companyId, UpdateBiometricSettingsDto dto, CancellationToken ct = default);

    // ── Dashboard ─────────────────────────────────────────────────────────
    Task<BiometricDashboardDto> GetDashboardAsync(int companyId, CancellationToken ct = default);
}
