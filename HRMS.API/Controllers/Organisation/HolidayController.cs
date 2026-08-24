using HRMS.Application.Common;
using HRMS.Application.DTOs.Holiday;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Organisation;

/// <summary>Holiday Calendar — global and company-specific public holidays.</summary>
[ApiController]
[Route("api/holidays")]
[Authorize(Policy = "RequireMfaCompleted")]
public class HolidayController : BaseController
{
    private readonly IHolidayService _svc;
    public HolidayController(IHolidayService svc) => _svc = svc;

    /// <summary>List holidays (filtered by year). Employees see their company + global holidays.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? year,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] bool? isOptional = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc")
    {
        var companyId = CallerCompanyIdOrNull;
        var result = await _svc.GetAllPagedAsync(
            companyId, year, page, pageSize,
            search, isOptional, sortBy, sortDirection);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<HolidayDto>>.Ok(result));
    }

    /// <summary>Get a single holiday by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull to enforce tenant check in service.
        var h = await _svc.GetByIdAsync(id, CallerCompanyIdOrNull);
        return h == null
            ? NotFound(ApiResponse.Fail("Holiday not found."))
            : Ok(ApiResponse<HolidayDto>.Ok(h));
    }

    /// <summary>Create a holiday (admin/superadmin).</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateHolidayDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        try
        {
            var companyId = CallerCompanyIdOrNull;
            var h = await _svc.CreateAsync(companyId, dto);
            // FIX: HTTP 201 Created for resource creation (was 200 OK).
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<HolidayDto>.Ok(h, "Holiday created."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Update a holiday (admin/superadmin).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateHolidayDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        try
        {
            // FIX [2] IDOR — pass CallerCompanyIdOrNull and isSuperAdmin.
            // Global records (CompanyId == null) may only be modified by SuperAdmin.
        var ok = await _svc.UpdateAsync(id, dto, CallerCompanyIdOrNull, User.IsInRole(AppRoles.SuperAdmin));
            return ok
                ? Ok(ApiResponse.Ok("Holiday updated."))
                : NotFound(ApiResponse.Fail("Holiday not found."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Soft-delete a holiday (admin/superadmin).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        // FIX [2] IDOR — pass CallerCompanyIdOrNull and isSuperAdmin.
        var ok = await _svc.DeleteAsync(id, CallerCompanyIdOrNull, User.IsInRole(AppRoles.SuperAdmin));
        return ok
            ? Ok(ApiResponse.Ok("Holiday removed."))
            : NotFound(ApiResponse.Fail("Holiday not found."));
    }
}
