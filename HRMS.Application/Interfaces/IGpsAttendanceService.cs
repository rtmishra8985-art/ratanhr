using HRMS.Application.Common;
using HRMS.Application.DTOs.GPS;

namespace HRMS.Application.Interfaces;

public interface IGpsAttendanceService
{
    // ── Employee-facing ────────────────────────────────────────────────────
    /// <summary>Validate location against geofences and return whether check-in is allowed.</summary>
    Task<GeofenceValidationDto> ValidateLocationAsync(int? companyId, double lat, double lon);

    /// <summary>Record GPS check-in metadata alongside the web attendance record.</summary>
    Task<AttendanceGpsDto> RecordCheckInAsync(string employeeId, int? companyId, int webAttendanceId, GpsCheckInDto dto, string? ipAddress);

    /// <summary>Record GPS check-out metadata.</summary>
    Task<AttendanceGpsDto> RecordCheckOutAsync(string employeeId, int? companyId, int webAttendanceId, GpsCheckOutDto dto, string? ipAddress);

    // ── Admin queries ──────────────────────────────────────────────────────
    Task<GpsDashboardDto> GetDashboardAsync(int? companyId);
    Task<PagedResult<AttendanceGpsDto>> GetLogsAsync(int? companyId, GpsReportFilterDto filter);
    Task<PagedResult<AttendanceGpsDto>> GetOutsideRadiusLogsAsync(int? companyId, GpsReportFilterDto filter);

    // ── GeoFence management ────────────────────────────────────────────────
    Task<List<GeoFenceDto>> GetGeoFencesAsync(int? companyId);
    Task<GeoFenceDto?> GetGeoFenceByIdAsync(int id, int? companyId);
    Task<GeoFenceDto> CreateGeoFenceAsync(int? companyId, string createdBy, CreateGeoFenceDto dto);
    Task<GeoFenceDto?> UpdateGeoFenceAsync(int id, int? companyId, string updatedBy, UpdateGeoFenceDto dto);
    Task<bool> DeleteGeoFenceAsync(int id, int? companyId);
    Task<bool> ToggleGeoFenceAsync(int id, int? companyId, bool isActive);
}
