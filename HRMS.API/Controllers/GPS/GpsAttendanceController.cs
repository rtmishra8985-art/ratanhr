using HRMS.Application.Common;
using HRMS.Application.DTOs.GPS;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.GPS;

/// <summary>
/// GPS attendance endpoints — integrates with the existing web attendance workflow.
/// Call these AFTER the core check-in/check-out via /api/attendance to attach GPS metadata.
/// </summary>
[ApiController]
[Route("api/gps")]
[Authorize(Policy = "RequireMfaCompleted")]
public class GpsAttendanceController : BaseController
{
    private readonly IGpsAttendanceService _gps;
    public GpsAttendanceController(IGpsAttendanceService gps) => _gps = gps;

    // ── Employee: location validation & events ─────────────────────────────

    /// <summary>
    /// Validate employee's current GPS position against active geofences.
    /// Call this before check-in to show the user whether they are in range.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateLocationDto dto)
    {
        var result = await _gps.ValidateLocationAsync(CallerCompanyIdOrNull, dto.Latitude, dto.Longitude);
        return Ok(ApiResponse<GeofenceValidationDto>.Ok(result));
    }

    /// <summary>
    /// Record GPS check-in metadata for an existing WebAttendance record.
    /// </summary>
    [HttpPost("checkin/{webAttendanceId:int}")]
    public async Task<IActionResult> CheckIn(int webAttendanceId, [FromBody] GpsCheckInDto dto)
    {
        var empId = EmployeeIdStr;
        var ip    = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _gps.RecordCheckInAsync(empId, CallerCompanyIdOrNull, webAttendanceId, dto, ip);
        return Ok(ApiResponse<AttendanceGpsDto>.Ok(result, "GPS check-in recorded."));
    }

    /// <summary>
    /// Record GPS check-out metadata for an existing WebAttendance record.
    /// </summary>
    [HttpPost("checkout/{webAttendanceId:int}")]
    public async Task<IActionResult> CheckOut(int webAttendanceId, [FromBody] GpsCheckOutDto dto)
    {
        var empId = EmployeeIdStr;
        var ip    = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _gps.RecordCheckOutAsync(empId, CallerCompanyIdOrNull, webAttendanceId, dto, ip);
        return Ok(ApiResponse<AttendanceGpsDto>.Ok(result, "GPS check-out recorded."));
    }

    // ── Admin: dashboard & reports ─────────────────────────────────────────

    /// <summary>Admin: live GPS dashboard with today's check-ins and live employee locations.</summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _gps.GetDashboardAsync(CallerCompanyIdOrNull);
        return Ok(ApiResponse<GpsDashboardDto>.Ok(result));
    }

    /// <summary>Admin: paginated GPS attendance logs with filters.</summary>
    [HttpGet("logs")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Logs([FromQuery] GpsReportFilterDto filter)
    {
        var result = await _gps.GetLogsAsync(CallerCompanyIdOrNull, filter);
        return Ok(ApiResponse<PagedResult<AttendanceGpsDto>>.Ok(result));
    }

    /// <summary>Admin: employees who checked in from outside any geofence radius.</summary>
    [HttpGet("outside-radius")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> OutsideRadius([FromQuery] GpsReportFilterDto filter)
    {
        var result = await _gps.GetOutsideRadiusLogsAsync(CallerCompanyIdOrNull, filter);
        return Ok(ApiResponse<PagedResult<AttendanceGpsDto>>.Ok(result));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private string EmployeeIdStr => User.FindFirst("employeeId")?.Value ?? string.Empty;
}

/// <summary>Simple lat/lon payload for the validate endpoint.</summary>
public class ValidateLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
