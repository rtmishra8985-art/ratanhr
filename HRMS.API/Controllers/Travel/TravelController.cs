using HRMS.Application.Common;
using HRMS.Application.DTOs.Travel;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Travel;

[ApiController]
[Route("api/travel")]
[Authorize(Policy = "RequireMfaCompleted")]
public class TravelController : BaseController
{
    private readonly ITravelService _service;
    public TravelController(ITravelService service) => _service = service;

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // FIX: Shadow BaseController.CompanyId (int, returns -1 for SuperAdmin) with
    // an int? version that returns null for SuperAdmin and the JWT claim value for
    // all other roles.  Service methods that receive null skip the tenant filter
    // (SuperAdmin cross-company view); a non-SuperAdmin whose claim is absent or
    // malformed gets null → service returns nothing rather than leaking company 0.
    // This closes cross-tenant data leaks on the three admin read endpoints below:
    //   GET /api/travel/dashboard
    //   GET /api/travel
    //   GET /api/travel/report
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? (int?)null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : null;

    // ── Dashboard ──────────────────────────────────────────────────────────

    /// <summary>Travel dashboard stats and charts for the current company.</summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Dashboard()
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetDashboardAsync(CompanyId);
        return Ok(ApiResponse<TravelDashboardDto>.Ok(result));
    }

    // ── Admin: list + reports ──────────────────────────────────────────────

    /// <summary>Admin: paginated list of all travel requests, with optional status filter.</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null)
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetAllAsync(CompanyId, page, pageSize, status);
        return Ok(ApiResponse<PagedResult<TravelDto>>.Ok(result));
    }

    /// <summary>Admin: filtered report for export or drill-down.</summary>
    [HttpGet("report")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Report([FromQuery] TravelReportFilterDto filter)
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetReportAsync(CompanyId, filter);
        return Ok(ApiResponse<PagedResult<TravelDto>>.Ok(result));
    }

    // ── Employee: own requests ─────────────────────────────────────────────

    /// <summary>Employee: list own travel requests.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var empId = EmployeeIdStr;
        var result = await _service.GetMyRequestsAsync(empId);
        return Ok(ApiResponse<List<TravelDto>>.Ok(result));
    }

    /// <summary>Get a single travel request by ID (company-scoped IDOR guard).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id, CompanyId);
        return result != null
            ? Ok(ApiResponse<TravelDto>.Ok(result))
            : NotFound(ApiResponse.Fail("Travel request not found."));
    }

    /// <summary>Employee: create a new travel request (starts as Draft).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTravelDto dto)
    {
        var empId = EmployeeIdStr;
        var result = await _service.CreateAsync(empId, CompanyId, dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<TravelDto>.Ok(result, "Travel request created."));
    }

    /// <summary>Employee: update a Draft travel request.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTravelDto dto)
    {
        var empId = EmployeeIdStr;
        var result = await _service.UpdateAsync(id, empId, CompanyId, dto);
        return result != null
            ? Ok(ApiResponse<TravelDto>.Ok(result, "Travel request updated."))
            : BadRequest(ApiResponse.Fail("Cannot update — request not found or not in Draft state."));
    }

    /// <summary>Employee: submit a Draft for approval (triggers Manager step).</summary>
    [HttpPatch("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var empId = EmployeeIdStr;
        var ok = await _service.SubmitAsync(id, empId);
        return ok ? Ok(ApiResponse.Ok("Submitted for Manager approval."))
                  : BadRequest(ApiResponse.Fail("Cannot submit — request not found or not in Draft state."));
    }

    /// <summary>Employee: update a Draft request.</summary>
    [HttpPatch("{id:int}/update")]
    public async Task<IActionResult> PatchUpdate(int id, [FromBody] UpdateTravelDto dto)
    {
        var empId = EmployeeIdStr;
        var result = await _service.UpdateAsync(id, empId, CompanyId, dto);
        return result != null
            ? Ok(ApiResponse<TravelDto>.Ok(result, "Travel request updated."))
            : BadRequest(ApiResponse.Fail("Cannot update — request not found or not in Draft state."));
    }

    /// <summary>Employee: cancel a request that is not yet Finance-approved.</summary>
    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var empId = EmployeeIdStr;
        var ok = await _service.CancelAsync(id, empId);
        return ok ? Ok(ApiResponse.Ok("Request cancelled."))
                  : BadRequest(ApiResponse.Fail("Cannot cancel — request is already completed or finance-approved."));
    }

    /// <summary>Approver (Manager / HR / Finance): approve, reject, or send back.</summary>
    [HttpPatch("{id:int}/decide")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Decide(int id, [FromBody] TravelDecisionDto dto)
    {
        var approverName = UserId.ToString();
        var ok = await _service.DecideAsync(id, UserId, approverName, CompanyId, dto);
        if (!ok)
            return NotFound(ApiResponse.Fail("Request not found, or no pending approval for this step."));
        var msg = dto.SendBack ? "Sent back." : (dto.Approve ? $"Approved by {dto.Step}." : $"Rejected by {dto.Step}.");
        return Ok(ApiResponse.Ok(msg));
    }

    /// <summary>Employee: soft-delete a Draft request.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var empId = EmployeeIdStr;
        var ok = await _service.DeleteAsync(id, empId);
        return ok ? Ok(ApiResponse.Ok("Deleted."))
                  : NotFound(ApiResponse.Fail("Request not found or not in Draft state."));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private string EmployeeIdStr => User.FindFirst("employeeId")?.Value ?? string.Empty;
}
