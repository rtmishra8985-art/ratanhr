using HRMS.Application.DTOs.Performance;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Performance;

[ApiController]
[Route("api/performance")]
[Authorize(Policy = "RequireMfaCompleted")]   // any authenticated user; individual actions narrow to specific roles
public class PerformanceController : BaseController
{
    private readonly IPerformanceService _svc;

    // FIX HIGH-SA4: Use CallerCompanyIdOrNull instead of CompanyId.
    // CompanyId returns -1 for superadmin (no companyId claim), which caused all queries
    // to return empty results. CallerCompanyIdOrNull returns null for superadmin so the
    // service skips the tenant filter and returns cross-company data as intended.
    private int? CallerCompanyId    => CallerCompanyIdOrNull;
    private int  ActorUserId        => UserId;
    private string ActorEmployeeId  => EmployeeId ?? "";

    public PerformanceController(IPerformanceService svc) => _svc = svc;

    // ── Dashboard ──────────────────────────────────────────────────────────
    [HttpGet("dashboard")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetDashboard()
    {
        var data = await _svc.GetPerformanceDashboardAsync(CallerCompanyId);
        return Ok(ApiResponse<object>.Ok(data));
    }

    // ── Performance Cycles ─────────────────────────────────────────────────
    // FIX HIGH-OOM2: Added page/pageSize so this endpoint is now paginated.
    // FIX HIGH-SA4: Passes CallerCompanyIdOrNull (int?) instead of CompanyId (int).
    [HttpGet("cycles")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> ListCycles(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var result = await _svc.ListCyclesAsync(CallerCompanyId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("cycles")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> CreateCycle([FromBody] CreateCycleDto dto)
    {
        var result = await _svc.CreateCycleAsync(dto, CallerCompanyId, ActorUserId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Performance cycle created."));
    }

    [HttpPut("cycles/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> UpdateCycle(int id, [FromBody] UpdateCycleDto dto)
    {
        try
        {
            var result = await _svc.UpdateCycleAsync(id, dto, CallerCompanyId);
            return Ok(ApiResponse<object>.Ok(result, "Cycle updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Cycle not found.")); }
    }

    [HttpDelete("cycles/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> DeleteCycle(int id)
    {
        var ok = await _svc.DeleteCycleAsync(id, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Cycle deleted.")) : NotFound(ApiResponse.Fail("Cycle not found."));
    }

    // ── Employee Goals ─────────────────────────────────────────────────────

    /// <summary>Admin view: all employees' goals with optional filters and pagination.</summary>
    [HttpGet("goals")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> ListGoals(
        [FromQuery] string? employeeId,
        [FromQuery] int?    cycleId,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 20,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "asc")
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var data = await _svc.ListGoalsAsync(CallerCompanyId, employeeId, cycleId, page, pageSize, sortBy, sortDirection, HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>Employee self-service: view own goals with optional cycle filter and pagination.</summary>
    [HttpGet("goals/my")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> MyGoals(
        [FromQuery] int? cycleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;
        var data = await _svc.ListGoalsAsync(CallerCompanyId, ActorEmployeeId, cycleId, page, pageSize, sortBy: null, sortDirection: "desc", ct: HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("goals")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> CreateGoal([FromBody] CreateGoalDto dto)
    {
        var result = await _svc.CreateGoalAsync(dto, CallerCompanyId, ActorUserId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Goal created."));
    }

    [HttpPut("goals/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> UpdateGoal(int id, [FromBody] UpdateGoalDto dto)
    {
        try
        {
            var result = await _svc.UpdateGoalAsync(id, dto, CallerCompanyId);
            return Ok(ApiResponse<object>.Ok(result, "Goal updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Goal not found.")); }
    }

    /// <summary>Employees may update progress on their own goals.</summary>
    [HttpPatch("goals/{id:int}/progress")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> UpdateGoalProgress(int id, [FromBody] UpdateGoalProgressDto dto)
    {
        var callerEmpId = User.IsInRole(AppRoles.Employee) ? ActorEmployeeId : null;
        var ok = await _svc.UpdateGoalProgressAsync(id, dto.AchievedValue, CallerCompanyId, callerEmpId);
        return ok ? Ok(ApiResponse.Ok("Progress updated.")) : NotFound(ApiResponse.Fail("Goal not found."));
    }

    [HttpDelete("goals/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> DeleteGoal(int id)
    {
        var ok = await _svc.DeleteGoalAsync(id, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Goal deleted.")) : NotFound(ApiResponse.Fail("Goal not found."));
    }

    // ── Performance Reviews ────────────────────────────────────────────────

    /// <summary>Admin view: all reviews with optional filters and pagination.</summary>
    // Medium FIX: ListReviews was unbounded — a company with thousands of employees would
    // return a massive payload. Results are now paginated via in-memory ToPagedResult().
    [HttpGet("reviews")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> ListReviews(
        [FromQuery] string? employeeId,
        [FromQuery] int?    cycleId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        // FIX [MEMORY]: pagination now happens at DB layer inside ListReviewsAsync
        var paged = await _svc.ListReviewsAsync(CallerCompanyId, employeeId, cycleId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(paged));
    }

    /// <summary>Employee self-service: view own reviews (paginated).</summary>
    [HttpGet("reviews/my")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> MyReviews(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var paged = await _svc.ListReviewsAsync(CallerCompanyId, ActorEmployeeId, page: page, pageSize: pageSize);
        return Ok(ApiResponse<object>.Ok(paged));
    }

    /// <summary>Employee self-service: view a single review (must belong to them or be admin).</summary>
    [HttpGet("reviews/{id:int}")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> GetReview(int id)
    {
        var data = await _svc.GetReviewAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Review not found."));
        if (User.IsInRole(AppRoles.Employee) && data.EmployeeId != ActorEmployeeId)
            return Forbid();
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("reviews")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
    {
        var result = await _svc.CreateReviewAsync(dto, CallerCompanyId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Review initiated."));
    }

    /// <summary>Employee self-service: submit self-assessment.</summary>
    [HttpPost("reviews/{id:int}/self")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> SubmitSelfReview(int id, [FromBody] SubmitSelfReviewDto dto)
    {
        var ok = await _svc.SubmitSelfReviewAsync(id, dto, CallerCompanyId, ActorEmployeeId);
        return ok ? Ok(ApiResponse.Ok("Self review submitted.")) : NotFound(ApiResponse.Fail("Review not found."));
    }

    [HttpPost("reviews/{id:int}/manager")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> SubmitManagerReview(int id, [FromBody] SubmitManagerReviewDto dto)
    {
        var ok = await _svc.SubmitManagerReviewAsync(id, dto, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Manager review submitted.")) : NotFound(ApiResponse.Fail("Review not found."));
    }

    [HttpPost("reviews/{id:int}/finalize")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> FinalizeReview(int id, [FromBody] FinalizeReviewDto dto)
    {
        var ok = await _svc.FinalizeReviewAsync(id, dto, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Review finalized.")) : NotFound(ApiResponse.Fail("Review not found."));
    }

    // ── Continuous Feedback ────────────────────────────────────────────────

    /// <summary>Admin view: list all feedback, optionally filtered by recipient.</summary>
    // FIX HIGH-OOM3: Added page/pageSize so this endpoint is now paginated.
    // FIX HIGH-SA4: Passes CallerCompanyIdOrNull (int?) instead of CompanyId (int).
    [HttpGet("feedback")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> ListFeedback(
        [FromQuery] string? toEmployeeId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var result = await _svc.ListFeedbackAsync(CallerCompanyId, toEmployeeId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>Employee self-service: view feedback sent to the caller (paginated).</summary>
    // FIX HIGH-OOM3: MyFeedback endpoint is also paginated for consistency.
    [HttpGet("feedback/my")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> MyFeedback(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var result = await _svc.ListFeedbackAsync(CallerCompanyId, ActorEmployeeId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>Any authenticated user can submit feedback.</summary>
    [HttpPost("feedback")]
    [Authorize(Roles = AppRoles.AdminSuperAdminEmployee)]
    public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackDto dto)
    {
        var result = await _svc.SubmitFeedbackAsync(dto, CallerCompanyId, ActorEmployeeId);
        return Ok(ApiResponse<object>.Ok(result, "Feedback submitted."));
    }
}
