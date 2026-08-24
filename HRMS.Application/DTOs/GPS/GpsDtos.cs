namespace HRMS.Application.DTOs.GPS;

// ── GPS Check-In / Out ────────────────────────────────────────────────────────

public class GpsCheckInDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }

    // Device context — all optional; browser supplies what it can
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? Network { get; set; }
    public double? BatteryLevel { get; set; }
    public string? GpsStatus { get; set; }
    public string? DeviceFingerprint { get; set; }
}

public class GpsCheckOutDto : GpsCheckInDto { }

// ── GPS Log read model ─────────────────────────────────────────────────────────

public class AttendanceGpsDto
{
    public int Id { get; set; }
    public int WebAttendanceId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int? GeoFenceId { get; set; }
    public string? GeoFenceName { get; set; }
    public double? DistanceMetres { get; set; }
    public bool IsInsideGeofence { get; set; }
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? IpAddress { get; set; }
    public string? Network { get; set; }
    public double? BatteryLevel { get; set; }
    public string? GpsStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── GeoFence CRUD ─────────────────────────────────────────────────────────────

public class CreateGeoFenceDto
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Office | Factory | Warehouse | Branch | ProjectSite | Store</summary>
    public string FenceType { get; set; } = "Office";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>50 | 100 | 200 | 500 | 1000</summary>
    public double RadiusMetres { get; set; } = 200;
    public int? BranchId { get; set; }
    public string? Address { get; set; }
    public bool AllowOutsideCheckin { get; set; }
}

public class UpdateGeoFenceDto : CreateGeoFenceDto { }

public class GeoFenceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FenceType { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMetres { get; set; }
    public int? BranchId { get; set; }
    public string? Address { get; set; }
    public bool AllowOutsideCheckin { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ── Admin dashboard ───────────────────────────────────────────────────────────

public class GpsDashboardDto
{
    public int TodayCheckedIn { get; set; }
    public int TodayInsideGeofence { get; set; }
    public int TodayOutsideGeofence { get; set; }
    public int TodayDenied { get; set; }
    public int LateEmployees { get; set; }
    public List<LiveEmployeeLocationDto> LiveLocations { get; set; } = new();
}

public class LiveEmployeeLocationDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsInsideGeofence { get; set; }
    public DateTime LastSeen { get; set; }
}

// ── Reports ───────────────────────────────────────────────────────────────────

public class GpsReportFilterDto
{
    public string? EmployeeId { get; set; }
    public int? GeoFenceId { get; set; }
    public bool? InsideOnly { get; set; }
    public bool? OutsideOnly { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// ── Geofence validation response ──────────────────────────────────────────────

public class GeofenceValidationDto
{
    public bool IsInsideGeofence { get; set; }
    public bool CanCheckIn { get; set; }
    public double? DistanceMetres { get; set; }
    public GeoFenceDto? MatchedFence { get; set; }
    public string? Message { get; set; }
}
