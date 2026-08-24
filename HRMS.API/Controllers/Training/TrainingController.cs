using HRMS.Application.Common;
using HRMS.Application.DTOs.Training;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Training;

[ApiController]
[Route("api/training")]
[Authorize(Policy = "RequireMfaCompleted")]
public class TrainingController : BaseController
{
    private readonly ITrainingService _service;
    public TrainingController(ITrainingService service) => _service = service;

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // FIX: Shadow BaseController.CompanyId (int, returns -1 for SuperAdmin) with
    // an int? version that returns null for SuperAdmin and the JWT claim value for
    // all other roles.  Service methods that receive null skip the tenant filter
    // (SuperAdmin cross-company view); a non-SuperAdmin whose claim is absent or
    // malformed gets null → service returns nothing rather than leaking company 0.
    // This closes cross-tenant data leaks on the two read endpoints below:
    //   GET /api/training
    //   GET /api/training/{id}
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? (int?)null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : null;

    // ── Read endpoints (scoped) ────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetAllAsync(CompanyId, page, pageSize);
        return Ok(ApiResponse<PagedResult<TrainingDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetByIdAsync(id, CompanyId);
        return result != null
            ? Ok(ApiResponse<TrainingDto>.Ok(result))
            : NotFound(ApiResponse.Fail("Training program not found."));
    }

    // ── Write endpoints ────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateTrainingDto dto)
    {
        var result = await _service.CreateAsync(CompanyId, dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<TrainingDto>.Ok(result, "Training program created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTrainingDto dto)
    {
        var ok = await _service.UpdateAsync(id, CompanyId, dto);
        return ok ? Ok(ApiResponse.Ok("Updated.")) : NotFound(ApiResponse.Fail("Not found."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _service.DeleteAsync(id, CompanyId);
        return ok ? Ok(ApiResponse.Ok("Deleted.")) : NotFound(ApiResponse.Fail("Not found."));
    }

    /// <summary>
    /// Enroll an employee in a training program.
    /// Returns 403 Forbidden when the employee belongs to a different company than the training program.
    /// </summary>
    [HttpPost("{id:int}/enroll")]
    public async Task<IActionResult> Enroll(int id, [FromBody] EnrollDto dto)
    {
        var (ok, message, isCrossTenant) = await _service.EnrollAsync(id, dto.EmployeeId);

        if (isCrossTenant)
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Access denied: cross-tenant enrollment is not permitted."));

        return ok
            ? Ok(ApiResponse.Ok(message))
            : BadRequest(ApiResponse.Fail(message));
    }

    [HttpGet("enrollments/my")]
    public async Task<IActionResult> GetMyEnrollments()
    {
        var empId = User.FindFirst("employeeId")?.Value ?? "";
        var result = await _service.GetEnrollmentsByEmployeeAsync(empId);
        return Ok(ApiResponse<List<EnrollmentDto>>.Ok(result));
    }

    [HttpPatch("enrollments/{enrollmentId:int}/complete")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> MarkComplete(int enrollmentId, [FromBody] MarkCompleteDto dto)
    {
        var ok = await _service.MarkCompleteAsync(enrollmentId, CompanyId, dto);
        return ok ? Ok(ApiResponse.Ok("Marked complete.")) : NotFound(ApiResponse.Fail("Enrollment not found."));
    }
}
