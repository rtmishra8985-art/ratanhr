using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces.Biometric;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Application service for biometric device management.
/// Orchestrates device CRUD, connectivity testing, sync history, settings and dashboard data.
/// Reuses existing IBiometricProviderFactory and IBiometricSyncService — does NOT re-implement them.
/// </summary>
public sealed class BiometricDeviceService : IBiometricDeviceService
{
    private readonly IBiometricDeviceRepository      _deviceRepo;
    private readonly IBiometricLogRepository         _logRepo;
    private readonly IBiometricSyncHistoryRepository _historyRepo;
    private readonly BiometricSettingsRepository     _settingsRepo;
    private readonly IBiometricProviderFactory       _factory;
    private readonly ILogger<BiometricDeviceService> _logger;

    public BiometricDeviceService(
        IBiometricDeviceRepository      deviceRepo,
        IBiometricLogRepository         logRepo,
        IBiometricSyncHistoryRepository historyRepo,
        BiometricSettingsRepository     settingsRepo,
        IBiometricProviderFactory       factory,
        ILogger<BiometricDeviceService> logger)
    {
        _deviceRepo  = deviceRepo;
        _logRepo     = logRepo;
        _historyRepo = historyRepo;
        _settingsRepo = settingsRepo;
        _factory     = factory;
        _logger      = logger;
    }

    // ── Device CRUD ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BiometricDeviceDto>> GetDevicesAsync(int companyId, CancellationToken ct = default)
    {
        var devices = await _deviceRepo.GetAllAsync(companyId, ct);
        return devices.Select(MapDevice).ToList();
    }

    public async Task<BiometricDeviceDto?> GetDeviceByIdAsync(int id, int companyId, CancellationToken ct = default)
    {
        var device = await _deviceRepo.GetByIdAsync(id, companyId, ct);
        return device is null ? null : MapDevice(device);
    }

    public async Task<BiometricDeviceDto> CreateDeviceAsync(int companyId, CreateBiometricDeviceDto dto, CancellationToken ct = default)
    {
        // Validate that the requested vendor is registered
        _factory.GetProvider(dto.ProviderType.ToString()); // throws NotSupportedException if unknown

        var entity = new BiometricDevice
        {
            CompanyId        = companyId,
            Name             = dto.Name,
            ProviderType     = dto.ProviderType,
            VendorName       = dto.ProviderType.ToString(),
            IpAddress        = dto.IpAddress,
            Port             = dto.Port,
            SerialNumber     = dto.SerialNumber,
            Location         = dto.Location,
            ConnectionParams = dto.ConnectionParams,
            Status           = BiometricStatus.Active,
            IsEnabled        = true,
        };

        var created = await _deviceRepo.AddAsync(entity, ct);
        _logger.LogInformation("BiometricDevice created: Id={Id}, Company={Company}, Vendor={Vendor}", created.Id, companyId, created.VendorName);
        return MapDevice(created);
    }

    public async Task<BiometricDeviceDto> UpdateDeviceAsync(int id, int companyId, UpdateBiometricDeviceDto dto, CancellationToken ct = default)
    {
        var entity = await _deviceRepo.GetByIdAsync(id, companyId, ct)
            ?? throw new KeyNotFoundException($"BiometricDevice {id} not found.");

        entity.Name             = dto.Name;
        entity.IpAddress        = dto.IpAddress;
        entity.Port             = dto.Port;
        entity.SerialNumber     = dto.SerialNumber;
        entity.Location         = dto.Location;
        entity.IsEnabled        = dto.IsEnabled;
        entity.ConnectionParams = dto.ConnectionParams;

        await _deviceRepo.UpdateAsync(entity, ct);
        return MapDevice(entity);
    }

    public async Task DeleteDeviceAsync(int id, int companyId, CancellationToken ct = default)
    {
        await _deviceRepo.DeleteAsync(id, companyId, ct);
        _logger.LogInformation("BiometricDevice deleted: Id={Id}, Company={Company}", id, companyId);
    }

    // ── Device Control ────────────────────────────────────────────────────

    public async Task EnableDeviceAsync(int id, int companyId, CancellationToken ct = default)
    {
        var entity = await _deviceRepo.GetByIdAsync(id, companyId, ct)
            ?? throw new KeyNotFoundException($"BiometricDevice {id} not found.");
        entity.IsEnabled = true;
        entity.Status    = BiometricStatus.Active;
        await _deviceRepo.UpdateAsync(entity, ct);
    }

    public async Task DisableDeviceAsync(int id, int companyId, CancellationToken ct = default)
    {
        var entity = await _deviceRepo.GetByIdAsync(id, companyId, ct)
            ?? throw new KeyNotFoundException($"BiometricDevice {id} not found.");
        entity.IsEnabled = false;
        entity.Status    = BiometricStatus.Disabled;
        await _deviceRepo.UpdateAsync(entity, ct);
    }

    public async Task<BiometricDeviceStatusDto> TestConnectionAsync(int id, int companyId, CancellationToken ct = default)
    {
        var entity = await _deviceRepo.GetByIdAsync(id, companyId, ct)
            ?? throw new KeyNotFoundException($"BiometricDevice {id} not found.");

        var provider  = _factory.GetProvider(entity.VendorName);
        var status    = await provider.GetDeviceStatusAsync(ct);
        var pingAt    = DateTime.UtcNow;
        var newStatus = status.IsOnline ? BiometricStatus.Active : BiometricStatus.Unreachable;

        // Persist firmware/enrolled count back to device record
        entity.FirmwareVersion    = status.FirmwareVersion ?? entity.FirmwareVersion;
        entity.EnrolledUserCount  = status.EnrolledUserCount ?? entity.EnrolledUserCount;
        entity.Status             = newStatus;
        entity.LastError          = status.LastError;
        entity.LastPingAt         = pingAt;
        await _deviceRepo.UpdateAsync(entity, ct);

        return new BiometricDeviceStatusDto(
            entity.Id,
            entity.Name,
            entity.VendorName,
            status.IsOnline,
            status.FirmwareVersion,
            status.EnrolledUserCount,
            status.LastError,
            pingAt);
    }

    // ── Logs ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<BiometricLogDto>> GetLogsAsync(int companyId, BiometricLogFilterDto filter, CancellationToken ct = default)
    {
        var paged = await _logRepo.GetPagedAsync(
            companyId, filter.DeviceId, filter.UserId, filter.From, filter.To, filter.IsProcessed,
            filter.Page, filter.PageSize, ct);

        var items = paged.Items.Select(l => new BiometricLogDto(
            l.Id,
            l.BiometricDeviceId,
            l.Device?.Name ?? "-",
            l.UserId,
            l.CompanyId,
            l.PunchedAt,
            l.Direction.ToString(),
            l.DeviceSerial,
            l.IsProcessed,
            l.WebAttendanceId,
            l.SkipReason,
            l.CreatedAt)).ToList();

        return PagedResult<BiometricLogDto>.Create(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    // ── Sync History ──────────────────────────────────────────────────────

    public async Task<PagedResult<BiometricSyncHistoryDto>> GetSyncHistoryAsync(int companyId, int? deviceId, int page, int pageSize, CancellationToken ct = default)
    {
        var paged = await _historyRepo.GetPagedAsync(companyId, deviceId, page, pageSize, ct);
        var items = paged.Items.Select(h => new BiometricSyncHistoryDto(
            h.Id,
            h.BiometricDeviceId,
            h.Device?.Name,
            h.VendorName,
            h.RangeFrom,
            h.RangeTo,
            h.StartedAt,
            h.CompletedAt,
            h.TotalFetched,
            h.RecordsCreated,
            h.RecordsUpdated,
            h.RecordsSkipped,
            h.IsSuccess,
            h.ErrorMessage,
            h.IsAutomatic,
            h.TriggeredByUserId,
            h.CompletedAt.HasValue ? (h.CompletedAt.Value - h.StartedAt).TotalSeconds : null
        )).ToList();

        return PagedResult<BiometricSyncHistoryDto>.Create(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    // ── Settings ──────────────────────────────────────────────────────────

    public async Task<BiometricSettingsDto> GetSettingsAsync(int companyId, CancellationToken ct = default)
    {
        var s = await _settingsRepo.GetOrCreateAsync(companyId, ct);
        return MapSettings(s);
    }

    public async Task<BiometricSettingsDto> UpdateSettingsAsync(int companyId, UpdateBiometricSettingsDto dto, CancellationToken ct = default)
    {
        var s = await _settingsRepo.GetOrCreateAsync(companyId, ct);
        s.AutoSyncEnabled                = dto.AutoSyncEnabled;
        s.SyncIntervalMinutes            = dto.SyncIntervalMinutes;
        s.SyncLookbackDays               = dto.SyncLookbackDays;
        s.GraceTimeMinutes               = dto.GraceTimeMinutes;
        s.MinHalfDayHours                = dto.MinHalfDayHours;
        s.EnableDuplicatePunchDetection  = dto.EnableDuplicatePunchDetection;
        s.DedupeWindowMinutes            = dto.DedupeWindowMinutes;
        s.QueueUnknownEmployees          = dto.QueueUnknownEmployees;
        s.RealtimeEnabled                = dto.RealtimeEnabled;
        s.PersistRawLogs                 = dto.PersistRawLogs;
        s.LogRetentionDays               = dto.LogRetentionDays;
        await _settingsRepo.UpdateAsync(s, ct);
        return MapSettings(s);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────

    public async Task<BiometricDashboardDto> GetDashboardAsync(int companyId, CancellationToken ct = default)
    {
        var devices    = await _deviceRepo.GetAllAsync(companyId, ct);
        var latest     = await _historyRepo.GetLatestAsync(companyId, ct);
        var todayPunches   = await _logRepo.CountTodayAsync(companyId, ct);
        var unprocessed    = await _logRepo.CountUnprocessedAsync(companyId, ct);

        var statuses = await Task.WhenAll(devices.Select(async d =>
        {
            try
            {
                var p = _factory.GetProvider(d.VendorName);
                var s = await p.GetDeviceStatusAsync(ct);
                return new BiometricDeviceStatusDto(d.Id, d.Name, d.VendorName, s.IsOnline, s.FirmwareVersion, s.EnrolledUserCount, s.LastError, DateTime.UtcNow);
            }
            catch
            {
                return new BiometricDeviceStatusDto(d.Id, d.Name, d.VendorName, false, null, null, "Provider error", DateTime.UtcNow);
            }
        }));

        return new BiometricDashboardDto(
            devices.Count,
            statuses.Count(s => s.IsOnline),
            statuses.Count(s => !s.IsOnline && devices.First(d => d.Id == s.DeviceId).IsEnabled),
            devices.Count(d => !d.IsEnabled || d.Status == BiometricStatus.Disabled),
            todayPunches,
            latest?.RecordsCreated ?? 0,
            unprocessed,
            latest?.RecordsCreated ?? 0,
            latest?.StartedAt,
            statuses);
    }

    // ── Mapping Helpers ───────────────────────────────────────────────────

    private static BiometricDeviceDto MapDevice(BiometricDevice d) => new(
        d.Id, d.CompanyId, d.Name, d.ProviderType, d.VendorName,
        d.IpAddress, d.Port, d.SerialNumber, d.Location,
        d.Status, d.IsEnabled, d.LastSyncAt, d.LastPingAt,
        d.LastError, d.FirmwareVersion, d.EnrolledUserCount,
        d.CreatedAt, d.UpdatedAt);

    private static BiometricSettingsDto MapSettings(Domain.Entities.Attendance.BiometricSettings s) => new(
        s.Id, s.CompanyId, s.AutoSyncEnabled, s.SyncIntervalMinutes,
        s.SyncLookbackDays, s.GraceTimeMinutes, s.MinHalfDayHours,
        s.EnableDuplicatePunchDetection, s.DedupeWindowMinutes,
        s.QueueUnknownEmployees, s.RealtimeEnabled, s.PersistRawLogs,
        s.LogRetentionDays, s.UpdatedAt);
}
