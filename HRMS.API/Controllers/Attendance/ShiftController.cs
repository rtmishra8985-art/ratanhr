using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Attendance;

[ApiController]
[Route("api/shifts")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class ShiftController : BaseController
{
    private readonly IShiftService _svc;

    public ShiftController(IShiftService svc) => _svc = svc;

    // PHASE 2 FIX (P2-SHIFT-IDOR):
    // Previous behaviour: when a non-SuperAdmin supplied companyIdOverride, the
    // value was silently ignored and the caller's own company was used instead.
    // This hid the unauthorised override attempt instead of rejecting it.
    //
    // New behaviour:
    //   • Non-SuperAdmin + no override     → own company (JWT claim).
    //   • Non-SuperAdmin + override EQUALS own company → own company (allowed).
    //   • Non-SuperAdmin + override DIFFERS from own company → HTTP 403.
    //   • SuperAdmin + any override        → use override value.
    //   • SuperAdmin + no override         → own company claim (may be absent → all).
    //   • Missing company claim (non-superadmin) → HTTP 403 (IsCompanyClaimValid guard).
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? companyIdOverride,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        int? effectiveCompanyId;

        if (User.IsInRole(AppRoles.SuperAdmin))
        {
            // SuperAdmin may inspect any tenant. Per the docstring above: no override
            // falls back to the SuperAdmin's own companyId claim when present (e.g. a
            // SuperAdmin account that is also anchored to a home company), and only to
            // "all companies" (null) when the claim is genuinely absent.
            // BUG FIX: previously fell back to CompanyId (BaseController's raw property,
            // which returns -1 — not null — when the claim is missing), so a claim-less
            // SuperAdmin token made GetShiftsPagedAsync filter on the impossible
            // company_id = -1 and silently return an empty page instead of "all companies".
            // Parse the claim directly here (null on missing/malformed) rather than reusing
            // CompanyId (-1 sentinel) or CallerCompanyIdOrNull (always null for SuperAdmin,
            // which would ignore a present claim and break the "own company by default"
            // behaviour this endpoint documents and HRMS.Tests.IDOR.ShiftControllerIDORTests
            // asserts).
            int? claimCompanyId = int.TryParse(User.FindFirst("companyId")?.Value, out var scid)
                ? scid
                : null;
            effectiveCompanyId = companyIdOverride ?? claimCompanyId;
        }
        else
        {
            // Non-SuperAdmin: company claim must be present.
            if (!IsCompanyClaimValid)
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.Fail("Company identity could not be determined from your token."));

            var callerCompany = CompanyId;

            if (companyIdOverride.HasValue && companyIdOverride.Value != callerCompany)
            {
                // Explicit attempt to cross tenant boundary — reject.
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.Fail("You are not authorised to access shifts for another company."));
            }

            effectiveCompanyId = callerCompany;
        }

        var result = await _svc.GetShiftsPagedAsync(effectiveCompanyId, page, pageSize);
        return Ok(ApiResponse<PagedResult<ShiftDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShiftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));

        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            if (!IsCompanyClaimValid) return Forbid();
            dto.CompanyId = CallerCompanyIdOrNull!.Value;
        }

        var id = await _svc.CreateShiftAsync(dto);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { Id = id }, "Shift created."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateShiftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail(ModelState));
        var ok = await _svc.UpdateShiftAsync(id, dto, CallerCompanyIdOrNull);
        return ok
            ? Ok(ApiResponse.Ok("Shift updated."))
            : NotFound(ApiResponse.Fail("Shift not found."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteShiftAsync(id, CallerCompanyIdOrNull);
        return ok
            ? Ok(ApiResponse.Ok("Shift deleted."))
            : NotFound(ApiResponse.Fail("Shift not found."));
    }
}
