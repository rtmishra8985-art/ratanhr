using HRMS.Application.DTOs.Recruitment;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;


namespace HRMS.API.Controllers.Recruitment;

[ApiController]
[Route("api/recruitment")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class RecruitmentController : BaseController
{
    private readonly IRecruitmentService _svc;
    private readonly HRMS.Infrastructure.FileStorage.IFileStorageService _fileStorage;

    // FIX HIGH-SA4: Use CallerCompanyIdOrNull instead of CompanyId.
    // CompanyId returns -1 for superadmin (no companyId claim), which silently produced
    // empty results. CallerCompanyIdOrNull returns null for superadmin so the service
    // skips the tenant filter and returns all-company data as expected.
    private int? CallerCompanyId => CallerCompanyIdOrNull;
    private int  ActorUserId     => UserId;

    public RecruitmentController(
        IRecruitmentService svc,
        HRMS.Infrastructure.FileStorage.IFileStorageService fileStorage)
    {
        _svc         = svc;
        _fileStorage = fileStorage;
    }

    // ── Dashboard ──────────────────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var data = await _svc.GetRecruitmentDashboardAsync(CallerCompanyId);
        return Ok(ApiResponse<object>.Ok(data));
    }

    // ── Job Requisitions ───────────────────────────────────────────────────
    // Pagination is applied in the service query before materialisation.
    [HttpGet("requisitions")]
    public async Task<IActionResult> ListRequisitions(
        [FromQuery] string? status   = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var paged = await _svc.ListRequisitionsPagedAsync(CallerCompanyId, status, page, pageSize, HttpContext.RequestAborted);
        return Ok(ApiResponse<PagedResult<RequisitionListDto>>.Ok(paged));
    }

    [HttpPost("requisitions")]
    public async Task<IActionResult> CreateRequisition([FromBody] CreateRequisitionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CreateRequisitionAsync(dto, CallerCompanyId, ActorUserId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Job requisition created."));
    }

    [HttpGet("requisitions/{id:int}")]
    public async Task<IActionResult> GetRequisition(int id)
    {
        var data = await _svc.GetRequisitionAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Requisition not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPut("requisitions/{id:int}")]
    public async Task<IActionResult> UpdateRequisition(int id, [FromBody] UpdateRequisitionDto dto)
    {
        try
        {
            var result = await _svc.UpdateRequisitionAsync(id, dto, CallerCompanyId);
            return Ok(ApiResponse<object>.Ok(result, "Requisition updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Requisition not found.")); }
    }

    [HttpPatch("requisitions/{id:int}/status")]
    public async Task<IActionResult> UpdateRequisitionStatus(int id, [FromBody] UpdateRequisitionStatusDto dto)
    {
        var ok = await _svc.UpdateRequisitionStatusAsync(id, dto.Status, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Status updated.")) : NotFound(ApiResponse.Fail("Requisition not found."));
    }

    [HttpDelete("requisitions/{id:int}")]
    public async Task<IActionResult> DeleteRequisition(int id)
    {
        var ok = await _svc.DeleteRequisitionAsync(id, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Requisition deleted.")) : NotFound(ApiResponse.Fail("Requisition not found."));
    }

    // ── Candidates ─────────────────────────────────────────────────────────
    [HttpGet("candidates")]
    public async Task<IActionResult> ListCandidates(
        [FromQuery] int?    requisitionId,
        [FromQuery] string? status,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "asc")
    {
        var result = await _svc.ListCandidatesAsync(CallerCompanyId, requisitionId, status, page, pageSize, sortBy, sortDirection, HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("candidates")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [EnableRateLimiting("upload")]   // BLOCKER-11: 20 uploads/min per IP
    public async Task<IActionResult> CreateCandidate([FromForm] CreateCandidateDto dto, IFormFile? resume)
    {
        string? resumePath = null;
        // Item 9: resumes are documents only (.pdf/.doc/.docx) — a signature mismatch,
        // spoofed extension or oversized file returns HTTP 400 with the reason.
        try { resumePath = await _fileStorage.SaveAsync(resume, "resumes", HRMS.Infrastructure.Security.UploadProfile.Resume); }
        catch (HRMS.Infrastructure.FileStorage.FileUploadValidationException ex)
            { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (HRMS.Infrastructure.Security.UploadValidationException ex)
            { return BadRequest(ApiResponse.Fail(ex.Message)); }

        var result = await _svc.CreateCandidateAsync(dto, resumePath, CallerCompanyId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Candidate added."));
    }

    [HttpGet("candidates/{id:int}")]
    public async Task<IActionResult> GetCandidate(int id)
    {
        var data = await _svc.GetCandidateAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Candidate not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPut("candidates/{id:int}")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [EnableRateLimiting("upload")]   // BLOCKER-11: 20 uploads/min per IP
    public async Task<IActionResult> UpdateCandidate(int id, [FromForm] UpdateCandidateDto dto, IFormFile? resume)
    {
        string? resumePath = null;
        // Item 9: resumes are documents only (.pdf/.doc/.docx) — a signature mismatch,
        // spoofed extension or oversized file returns HTTP 400 with the reason.
        try { resumePath = await _fileStorage.SaveAsync(resume, "resumes", HRMS.Infrastructure.Security.UploadProfile.Resume); }
        catch (HRMS.Infrastructure.FileStorage.FileUploadValidationException ex)
            { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (HRMS.Infrastructure.Security.UploadValidationException ex)
            { return BadRequest(ApiResponse.Fail(ex.Message)); }

        try
        {
            var result = await _svc.UpdateCandidateAsync(id, dto, resumePath, CallerCompanyId);
            return Ok(ApiResponse<object>.Ok(result, "Candidate updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Candidate not found.")); }
    }

    [HttpPatch("candidates/{id:int}/status")]
    public async Task<IActionResult> UpdateCandidateStatus(int id, [FromBody] UpdateCandidateStatusDto dto)
    {
        var ok = await _svc.UpdateCandidateStatusAsync(id, dto.Status, dto.Notes, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Candidate status updated.")) : NotFound(ApiResponse.Fail("Candidate not found."));
    }

    [HttpDelete("candidates/{id:int}")]
    public async Task<IActionResult> DeleteCandidate(int id)
    {
        var ok = await _svc.DeleteCandidateAsync(id, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Candidate deleted.")) : NotFound(ApiResponse.Fail("Candidate not found."));
    }

    // ── Interviews ─────────────────────────────────────────────────────────
    // Pagination is applied in the service query before materialisation.
    [HttpGet("interviews")]
    public async Task<IActionResult> ListInterviews(
        [FromQuery] int? candidateId,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var paged = await _svc.ListInterviewsPagedAsync(CallerCompanyId, candidateId, page, pageSize, HttpContext.RequestAborted);
        return Ok(ApiResponse<PagedResult<InterviewListDto>>.Ok(paged));
    }

    [HttpPost("interviews")]
    public async Task<IActionResult> ScheduleInterview([FromBody] ScheduleInterviewDto dto)
    {
        var result = await _svc.ScheduleInterviewAsync(dto, CallerCompanyId, ActorUserId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Interview scheduled."));
    }

    [HttpPut("interviews/{id:int}")]
    public async Task<IActionResult> UpdateInterview(int id, [FromBody] UpdateInterviewDto dto)
    {
        try
        {
            var result = await _svc.UpdateInterviewAsync(id, dto, CallerCompanyId);
            return Ok(ApiResponse<object>.Ok(result, "Interview updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Interview not found.")); }
    }

    [HttpPost("interviews/{id:int}/feedback")]
    public async Task<IActionResult> SubmitFeedback(int id, [FromBody] SubmitFeedbackDto dto)
    {
        var ok = await _svc.SubmitInterviewFeedbackAsync(id, dto, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Feedback submitted.")) : NotFound(ApiResponse.Fail("Interview not found."));
    }

    [HttpDelete("interviews/{id:int}")]
    public async Task<IActionResult> DeleteInterview(int id)
    {
        var ok = await _svc.DeleteInterviewAsync(id, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Interview deleted.")) : NotFound(ApiResponse.Fail("Interview not found."));
    }

    // ── Offer Letters ──────────────────────────────────────────────────────
    // FIX HIGH-OOM1: Added page/pageSize so this endpoint is now paginated.
    // FIX HIGH-SA4: Passes CallerCompanyIdOrNull (int?) instead of CompanyId (int).
    [HttpGet("offers")]
    public async Task<IActionResult> ListOffers(
        [FromQuery] int? candidateId,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var result = await _svc.ListOffersAsync(CallerCompanyId, candidateId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("offers/{id:int}")]
    public async Task<IActionResult> GetOffer(int id)
    {
        var data = await _svc.GetOfferAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Offer not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("offers")]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferDto dto)
    {
        var result = await _svc.CreateOfferAsync(dto, CallerCompanyId, ActorUserId);
        return StatusCode(201, ApiResponse<object>.Ok(result, "Offer letter created."));
    }

    [HttpPost("offers/{id:int}/approve")]
    public async Task<IActionResult> ApproveOffer(int id, [FromBody] ApproveOfferDto dto)
    {
        var ok = await _svc.ApproveOfferAsync(id, dto, CallerCompanyId, ActorUserId);
        return ok ? Ok(ApiResponse.Ok("Offer approved.")) : NotFound(ApiResponse.Fail("Offer not found."));
    }

    [HttpPatch("offers/{id:int}/status")]
    public async Task<IActionResult> UpdateOfferStatus(int id, [FromBody] UpdateOfferStatusDto dto)
    {
        var ok = await _svc.UpdateOfferStatusAsync(id, dto.Status, CallerCompanyId);
        return ok ? Ok(ApiResponse.Ok("Offer status updated.")) : NotFound(ApiResponse.Fail("Offer not found."));
    }
}
