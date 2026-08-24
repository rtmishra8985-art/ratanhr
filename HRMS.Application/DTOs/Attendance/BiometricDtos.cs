using HRMS.Domain.Enums;

namespace HRMS.Application.DTOs.Attendance;

// ── Device DTOs ───────────────────────────────────────────────────────────────

public sealed record BiometricDeviceDto(
    int                 Id,
    int?                CompanyId,
    string              Name,
    BiometricProviderType ProviderType,
    string              VendorName,
    string              IpAddress,
    int                 Port,
    string?             SerialNumber,
    string?             Location,
    BiometricStatus     Status,
    bool                IsEnabled,
    DateTime?           LastSyncAt,
    DateTime?           LastPingAt,
    string?             LastError,
    string?             FirmwareVersion,
    int?                EnrolledUserCount,
    DateTime            CreatedAt,
    DateTime            UpdatedAt);

public sealed record CreateBiometricDeviceDto(
    string              Name,
    BiometricProviderType ProviderType,
    string              IpAddress,
    int                 Port,
    string?             SerialNumber,
    string?             Location,
    string?             ConnectionParams);

public sealed record UpdateBiometricDeviceDto(
    string              Name,
    string              IpAddress,
    int                 Port,
    string?             SerialNumber,
    string?             Location,
    bool                IsEnabled,
    string?             ConnectionParams);

public sealed record BiometricDeviceStatusDto(
    int     DeviceId,
    string  DeviceName,
    string  VendorName,
    bool    IsOnline,
    string? FirmwareVersion,
    int?    EnrolledUserCount,
    string? LastError,
    DateTime CheckedAt);

// ── Log DTOs ──────────────────────────────────────────────────────────────────

public sealed record BiometricLogDto(
    int      Id,
    int      BiometricDeviceId,
    string   DeviceName,
    string   UserId,
    int?     CompanyId,
    DateTime PunchedAt,
    string   Direction,
    string?  DeviceSerial,
    bool     IsProcessed,
    int?     WebAttendanceId,
    string?  SkipReason,
    DateTime CreatedAt);

public sealed record BiometricLogFilterDto(
    int?      DeviceId,
    string?   UserId,
    DateTime? From,
    DateTime? To,
    bool?     IsProcessed,
    int       Page    = 1,
    int       PageSize = 50);

// ── Sync History DTOs ─────────────────────────────────────────────────────────

public sealed record BiometricSyncHistoryDto(
    int       Id,
    int?      BiometricDeviceId,
    string?   DeviceName,
    string    VendorName,
    DateTime  RangeFrom,
    DateTime  RangeTo,
    DateTime  StartedAt,
    DateTime? CompletedAt,
    int       TotalFetched,
    int       RecordsCreated,
    int       RecordsUpdated,
    int       RecordsSkipped,
    bool      IsSuccess,
    string?   ErrorMessage,
    bool      IsAutomatic,
    int?      TriggeredByUserId,
    double?   DurationSeconds);

// ── Settings DTOs ─────────────────────────────────────────────────────────────

public sealed record BiometricSettingsDto(
    int     Id,
    int?    CompanyId,
    bool    AutoSyncEnabled,
    int     SyncIntervalMinutes,
    int     SyncLookbackDays,
    int     GraceTimeMinutes,
    int     MinHalfDayHours,
    bool    EnableDuplicatePunchDetection,
    int     DedupeWindowMinutes,
    bool    QueueUnknownEmployees,
    bool    RealtimeEnabled,
    bool    PersistRawLogs,
    int     LogRetentionDays,
    DateTime UpdatedAt);

public sealed record UpdateBiometricSettingsDto(
    bool    AutoSyncEnabled,
    int     SyncIntervalMinutes,
    int     SyncLookbackDays,
    int     GraceTimeMinutes,
    int     MinHalfDayHours,
    bool    EnableDuplicatePunchDetection,
    int     DedupeWindowMinutes,
    bool    QueueUnknownEmployees,
    bool    RealtimeEnabled,
    bool    PersistRawLogs,
    int     LogRetentionDays);

// ── Sync Result DTO ───────────────────────────────────────────────────────────

// BLOCKER-1 FIX: BiometricController.Sync returns ApiResponse<BiometricSyncResultDto>
// but this DTO was never defined. IBiometricSyncService.SyncAttendanceAsync returns
// Task<int> (count of records synced). The DTO wraps that count so the API response
// carries a structured body rather than a bare integer.
public sealed record BiometricSyncResultDto(
    /// <summary>Number of attendance records created or updated during the sync.</summary>
    int RecordsSynced);

// ── Dashboard DTO ─────────────────────────────────────────────────────────────

public sealed record BiometricDashboardDto(
    int     TotalDevices,
    int     OnlineDevices,
    int     OfflineDevices,
    int     DisabledDevices,
    int     TodayPunches,
    int     TodayNewAttendance,
    int     PendingUnknownEmployees,
    int     LastSyncRecordsCreated,
    DateTime? LastSyncAt,
    IReadOnlyList<BiometricDeviceStatusDto> DeviceStatuses);
