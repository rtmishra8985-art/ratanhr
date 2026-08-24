using HRMS.Application.Common;
using HRMS.Application.DTOs.GPS;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class GpsAttendanceService : IGpsAttendanceService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GpsAttendanceService> _logger;

    public GpsAttendanceService(ApplicationDbContext db, ILogger<GpsAttendanceService> logger)
    {
        _db = db; _logger = logger;
    }

    // ── Geofence helpers ───────────────────────────────────────────────────

    private async Task<(GeoFence? fence, double? distanceMetres)> FindNearestFenceAsync(
        int? companyId, double lat, double lon)
    {
        var fences = await _db.GeoFences
            .Where(f => !f.IsDeleted && f.IsActive && (companyId == null || f.CompanyId == companyId))
            .ToListAsync();

        GeoFence? nearest = null;
        double nearestDist = double.MaxValue;

        foreach (var f in fences)
        {
            var dist = GeoMath.HaversineMetres(lat, lon, f.Latitude, f.Longitude);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = f;
            }
        }
        return (nearest, nearest == null ? null : nearestDist);
    }

    // ── Employee-facing ────────────────────────────────────────────────────

    public async Task<GeofenceValidationDto> ValidateLocationAsync(int? companyId, double lat, double lon)
    {
        var (fence, dist) = await FindNearestFenceAsync(companyId, lat, lon);
        if (fence == null)
        {
            return new GeofenceValidationDto
            {
                IsInsideGeofence = true,
                CanCheckIn = true,
                Message = "No geofence configured — check-in allowed."
            };
        }

        var inside = dist <= fence.RadiusMetres;
        return new GeofenceValidationDto
        {
            IsInsideGeofence = inside,
            CanCheckIn = inside || fence.AllowOutsideCheckin,
            DistanceMetres = dist,
            MatchedFence = ToFenceDto(fence),
            Message = inside
                ? $"You are inside {fence.Name}."
                : $"You are {dist:F0} m away from {fence.Name}. Allowed radius: {fence.RadiusMetres} m."
        };
    }

    public async Task<AttendanceGpsDto> RecordCheckInAsync(
        string employeeId, int? companyId, int webAttendanceId, GpsCheckInDto dto, string? ipAddress)
        => await RecordEventAsync(employeeId, companyId, webAttendanceId, "CheckIn", dto, ipAddress);

    public async Task<AttendanceGpsDto> RecordCheckOutAsync(
        string employeeId, int? companyId, int webAttendanceId, GpsCheckOutDto dto, string? ipAddress)
        => await RecordEventAsync(employeeId, companyId, webAttendanceId, "CheckOut", dto, ipAddress);

    private async Task<AttendanceGpsDto> RecordEventAsync(
        string employeeId, int? companyId, int webAttendanceId,
        string eventType, GpsCheckInDto dto, string? ipAddress)
    {
        // FIX P2 (IDOR): confirm the referenced attendance row belongs to the caller before
        // attaching GPS/audit metadata to it. Without this an employee can attach location
        // evidence to another employee's attendance record by guessing webAttendanceId.
        var ownsAttendance = await _db.WebAttendances
            .AnyAsync(a => a.Id == webAttendanceId
                        && a.EmployeeId == employeeId
                        && (companyId == null || a.CompanyId == companyId));
        if (!ownsAttendance)
            throw new UnauthorizedAccessException("Attendance record does not belong to the caller.");

        var (fence, dist) = await FindNearestFenceAsync(companyId, dto.Latitude, dto.Longitude);
        var inside = fence == null || dist <= fence.RadiusMetres;

        var log = new AttendanceGps
        {
            CompanyId      = companyId,
            WebAttendanceId = webAttendanceId,
            EmployeeId     = employeeId,
            Latitude       = dto.Latitude,
            Longitude      = dto.Longitude,
            Accuracy       = dto.Accuracy,
            EventType      = eventType,
            Timestamp      = DateTime.UtcNow,
            GeoFenceId     = fence?.Id,
            DistanceMetres = dist,
            IsInsideGeofence = inside,
            DeviceType     = dto.DeviceType,
            Browser        = dto.Browser,
            IpAddress      = ipAddress,
            Network        = dto.Network,
            BatteryLevel   = dto.BatteryLevel,
            GpsStatus      = dto.GpsStatus,
            CreatedAt      = DateTime.UtcNow
        };
        _db.AttendanceGpsLogs.Add(log);

        // Audit record
        var wasAllowed = inside || (fence?.AllowOutsideCheckin ?? true);
        _db.AttendanceLocationAudits.Add(new AttendanceLocationAudit
        {
            CompanyId    = companyId,
            EmployeeId   = employeeId,
            Latitude     = dto.Latitude,
            Longitude    = dto.Longitude,
            Accuracy     = dto.Accuracy,
            GeoFenceId   = fence?.Id,
            DistanceMetres = dist,
            IsInsideGeofence = inside,
            WasAllowed   = wasAllowed,
            EventType    = eventType,
            IpAddress    = ipAddress,
            Browser      = dto.Browser,
            DeviceType   = dto.DeviceType,
            CreatedAt    = DateTime.UtcNow
        });

        // FIX: Write an AuditLog entry when a fence violation is denied so the security
        // audit trail captures refused check-in/out attempts, not just the location record.
        if (!wasAllowed)
        {
            _db.AuditLogs.Add(new HRMS.Domain.Entities.AuditLog
            {
                Action          = "GPS_FENCE_VIOLATION",
                EntityType      = "AttendanceGps",
                EntityId        = employeeId,
                PerformedByName = employeeId,
                IpAddress       = ipAddress,
                Details         = $"Employee {employeeId} attempted {eventType} outside allowed geofence " +
                                  $"'{fence?.Name}' (distance: {dist:F0} m, radius: {fence?.RadiusMetres} m). " +
                                  $"AllowOutsideCheckin=false — event denied.",
                Success         = false,
                OccurredAt      = DateTime.UtcNow
            });
        }

        // Update / upsert device fingerprint
        if (!string.IsNullOrWhiteSpace(dto.DeviceFingerprint))
        {
            var device = await _db.AttendanceDevices
                .FirstOrDefaultAsync(d => d.EmployeeId == employeeId
                                       && d.DeviceFingerprint == dto.DeviceFingerprint);
            if (device == null)
            {
                _db.AttendanceDevices.Add(new AttendanceDevice
                {
                    CompanyId         = companyId,
                    EmployeeId        = employeeId,
                    DeviceFingerprint = dto.DeviceFingerprint,
                    DeviceType        = dto.DeviceType,
                    Browser           = dto.Browser,
                    LastIpAddress     = ipAddress,
                    FirstSeenAt       = DateTime.UtcNow,
                    LastSeenAt        = DateTime.UtcNow
                });
            }
            else
            {
                device.LastSeenAt    = DateTime.UtcNow;
                device.LastIpAddress = ipAddress;
                device.UseCount++;
                device.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return ToGpsDto(log, fence?.Name);
    }

    // ── Admin queries ──────────────────────────────────────────────────────

    public async Task<GpsDashboardDto> GetDashboardAsync(int? companyId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var todayLogs = await _db.AttendanceGpsLogs
            .Where(g => g.CreatedAt >= todayStart && (companyId == null || g.CompanyId == companyId))
            .ToListAsync();

        var deniedToday = await _db.AttendanceLocationAudits
            .CountAsync(a => a.CreatedAt >= todayStart && !a.WasAllowed
                          && (companyId == null || a.CompanyId == companyId));

        var checkIns = todayLogs.Where(g => g.EventType == "CheckIn").ToList();
        var liveLocations = checkIns
            .GroupBy(g => g.EmployeeId)
            .Select(grp =>
            {
                var last = grp.OrderByDescending(x => x.Timestamp).First();
                return new LiveEmployeeLocationDto
                {
                    EmployeeId = last.EmployeeId,
                    Latitude   = last.Latitude,
                    Longitude  = last.Longitude,
                    IsInsideGeofence = last.IsInsideGeofence,
                    LastSeen   = last.Timestamp
                };
            }).ToList();

        return new GpsDashboardDto
        {
            TodayCheckedIn     = checkIns.Select(g => g.EmployeeId).Distinct().Count(),
            TodayInsideGeofence  = checkIns.Count(g => g.IsInsideGeofence),
            TodayOutsideGeofence = checkIns.Count(g => !g.IsInsideGeofence),
            TodayDenied        = deniedToday,
            LateEmployees      = 0, // hook to shift-based late logic if needed
            LiveLocations      = liveLocations
        };
    }

    public async Task<PagedResult<AttendanceGpsDto>> GetLogsAsync(int? companyId, GpsReportFilterDto filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize is < 1 or > 500) filter.PageSize = 50;
        var q = _db.AttendanceGpsLogs
            .Include(g => g.GeoFence)
            .Where(g => companyId == null || g.CompanyId == companyId)
            .Where(g => filter.EmployeeId == null || g.EmployeeId == filter.EmployeeId)
            .Where(g => filter.GeoFenceId == null || g.GeoFenceId == filter.GeoFenceId)
            .Where(g => filter.InsideOnly != true || g.IsInsideGeofence)
            .Where(g => filter.OutsideOnly != true || !g.IsInsideGeofence)
            .Where(g => filter.FromDate == null || g.Timestamp >= filter.FromDate)
            .Where(g => filter.ToDate == null || g.Timestamp <= filter.ToDate)
            .OrderByDescending(g => g.Timestamp);
        var total = await q.CountAsync();
        var items = await q.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return PagedResult<AttendanceGpsDto>.Create(
            items.Select(g => ToGpsDto(g, g.GeoFence?.Name)).ToList(), total, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<AttendanceGpsDto>> GetOutsideRadiusLogsAsync(int? companyId, GpsReportFilterDto filter)
    {
        filter.OutsideOnly = true;
        return await GetLogsAsync(companyId, filter);
    }

    // ── GeoFence management ────────────────────────────────────────────────

    public async Task<List<GeoFenceDto>> GetGeoFencesAsync(int? companyId)
    {
        var list = await _db.GeoFences
            .Where(f => !f.IsDeleted && (companyId == null || f.CompanyId == companyId))
            .OrderBy(f => f.Name)
            .ToListAsync();
        return list.Select(ToFenceDto).ToList();
    }

    public async Task<GeoFenceDto?> GetGeoFenceByIdAsync(int id, int? companyId)
    {
        var f = await _db.GeoFences.FirstOrDefaultAsync(x => x.Id == id);
        if (f == null || f.IsDeleted) return null;
        if (companyId.HasValue && f.CompanyId.HasValue && f.CompanyId != companyId) return null;
        return ToFenceDto(f);
    }

    public async Task<GeoFenceDto> CreateGeoFenceAsync(int? companyId, string createdBy, CreateGeoFenceDto dto)
    {
        var fence = new GeoFence
        {
            CompanyId          = companyId,
            Name               = dto.Name,
            FenceType          = dto.FenceType,
            Latitude           = dto.Latitude,
            Longitude          = dto.Longitude,
            RadiusMetres       = dto.RadiusMetres,
            BranchId           = dto.BranchId,
            Address            = dto.Address,
            AllowOutsideCheckin = dto.AllowOutsideCheckin,
            IsActive           = true,
            CreatedBy          = createdBy,
            CreatedAt          = DateTime.UtcNow
        };
        _db.GeoFences.Add(fence);
        await _db.SaveChangesAsync();

        _db.GeoFenceHistories.Add(new GeoFenceHistory
        {
            GeoFenceId = fence.Id, CompanyId = companyId,
            Action = "Created", ChangedBy = createdBy, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ToFenceDto(fence);
    }

    public async Task<GeoFenceDto?> UpdateGeoFenceAsync(int id, int? companyId, string updatedBy, UpdateGeoFenceDto dto)
    {
        var fence = await _db.GeoFences.FirstOrDefaultAsync(x => x.Id == id);
        if (fence == null || fence.IsDeleted) return null;
        if (companyId.HasValue && fence.CompanyId.HasValue && fence.CompanyId != companyId) return null;

        fence.Name               = dto.Name;
        fence.FenceType          = dto.FenceType;
        fence.Latitude           = dto.Latitude;
        fence.Longitude          = dto.Longitude;
        fence.RadiusMetres       = dto.RadiusMetres;
        fence.BranchId           = dto.BranchId;
        fence.Address            = dto.Address;
        fence.AllowOutsideCheckin = dto.AllowOutsideCheckin;
        fence.UpdatedBy          = updatedBy;
        fence.UpdatedAt          = DateTime.UtcNow;

        _db.GeoFenceHistories.Add(new GeoFenceHistory
        {
            GeoFenceId = id, CompanyId = companyId,
            Action = "Updated", ChangedBy = updatedBy, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ToFenceDto(fence);
    }

    public async Task<bool> DeleteGeoFenceAsync(int id, int? companyId)
    {
        var fence = await _db.GeoFences.FirstOrDefaultAsync(x => x.Id == id);
        if (fence == null || fence.IsDeleted) return false;
        if (companyId.HasValue && fence.CompanyId.HasValue && fence.CompanyId != companyId) return false;
        fence.IsDeleted = true;
        fence.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleGeoFenceAsync(int id, int? companyId, bool isActive)
    {
        var fence = await _db.GeoFences.FirstOrDefaultAsync(x => x.Id == id);
        if (fence == null || fence.IsDeleted) return false;
        if (companyId.HasValue && fence.CompanyId.HasValue && fence.CompanyId != companyId) return false;
        fence.IsActive  = isActive;
        fence.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static AttendanceGpsDto ToGpsDto(AttendanceGps g, string? fenceName) => new()
    {
        Id               = g.Id,
        WebAttendanceId  = g.WebAttendanceId,
        EmployeeId       = g.EmployeeId,
        Latitude         = g.Latitude,
        Longitude        = g.Longitude,
        Accuracy         = g.Accuracy,
        EventType        = g.EventType,
        Timestamp        = g.Timestamp,
        GeoFenceId       = g.GeoFenceId,
        GeoFenceName     = fenceName,
        DistanceMetres   = g.DistanceMetres,
        IsInsideGeofence = g.IsInsideGeofence,
        DeviceType       = g.DeviceType,
        Browser          = g.Browser,
        IpAddress        = g.IpAddress,
        Network          = g.Network,
        BatteryLevel     = g.BatteryLevel,
        GpsStatus        = g.GpsStatus,
        CreatedAt        = g.CreatedAt
    };

    private static GeoFenceDto ToFenceDto(GeoFence f) => new()
    {
        Id                 = f.Id,
        Name               = f.Name,
        FenceType          = f.FenceType,
        Latitude           = f.Latitude,
        Longitude          = f.Longitude,
        RadiusMetres       = f.RadiusMetres,
        BranchId           = f.BranchId,
        Address            = f.Address,
        AllowOutsideCheckin = f.AllowOutsideCheckin,
        IsActive           = f.IsActive,
        CreatedAt          = f.CreatedAt,
        UpdatedAt          = f.UpdatedAt
    };
}
