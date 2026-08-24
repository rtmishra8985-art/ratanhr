using HRMS.Application.Common;
using HRMS.Application.DTOs.GPS;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.GPS;

[ApiController]
[Route("api/geofences")]
[Authorize(Policy = "RequireMfaCompleted")]
public class GeoFenceController : BaseController
{
    private readonly IGpsAttendanceService _gps;
    public GeoFenceController(IGpsAttendanceService gps) => _gps = gps;

    /// <summary>List all active geofences for the company. Available to all authenticated users (needed for map display).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _gps.GetGeoFencesAsync(CallerCompanyIdOrNull);
        return Ok(ApiResponse<List<GeoFenceDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _gps.GetGeoFenceByIdAsync(id, CallerCompanyIdOrNull);
        return result != null
            ? Ok(ApiResponse<GeoFenceDto>.Ok(result))
            : NotFound(ApiResponse.Fail("Geofence not found."));
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateGeoFenceDto dto)
    {
        var createdBy = UserId.ToString();
        var result = await _gps.CreateGeoFenceAsync(CallerCompanyIdOrNull, createdBy, dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<GeoFenceDto>.Ok(result, "Geofence created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGeoFenceDto dto)
    {
        var updatedBy = UserId.ToString();
        var result = await _gps.UpdateGeoFenceAsync(id, CallerCompanyIdOrNull, updatedBy, dto);
        return result != null
            ? Ok(ApiResponse<GeoFenceDto>.Ok(result, "Geofence updated."))
            : NotFound(ApiResponse.Fail("Geofence not found."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _gps.DeleteGeoFenceAsync(id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Geofence deleted."))
                  : NotFound(ApiResponse.Fail("Geofence not found."));
    }

    [HttpPatch("{id:int}/toggle")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Toggle(int id, [FromBody] ToggleDto dto)
    {
        var ok = await _gps.ToggleGeoFenceAsync(id, CallerCompanyIdOrNull, dto.IsActive);
        return ok ? Ok(ApiResponse.Ok(dto.IsActive ? "Geofence activated." : "Geofence deactivated."))
                  : NotFound(ApiResponse.Fail("Geofence not found."));
    }
}

public class ToggleDto
{
    public bool IsActive { get; set; }
}
