using Hangfire;
using HRMS.API.Security;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Payroll;

/// <summary>
/// Payslip PDF endpoints — M-20: PDF generation is now asynchronous (202 Accepted).
/// FIX HIGH-PS5: ApplicationDbContext removed from this controller. All DB access now
/// goes through IPayslipService so the controller is testable and obeys the service layer.
///
/// FIX P3-1 (download token binding): the download token is now issued through
/// IPayslipDownloadTokenStore, bound to (payslipId, userId, companyId), single-use and
/// short-lived. Previously the token was an unbound GUID used only as a filename, so a
/// token issued for payslip A could be replayed against payslip B (and by another user),
/// and it stayed valid for as long as the generated file existed on disk.
///
/// Workflow:
///   1. POST /api/payslip/{payslipId}/pdf          → enqueue job, return 202 + { token, statusUrl }
///   2. GET  /api/payslip/{payslipId}/pdf/status/{token} → { status: "queued"|"ready"|"failed" }
///   3. GET  /api/payslip/{payslipId}/pdf/download/{token} → binary PDF (200, token consumed) or 404
/// </summary>
[ApiController]
[Route("api/payslip")]
[Authorize(Policy = "RequireMfaCompleted")]
public class PayslipController : BaseController
{
    private readonly IPayslipService            _payslipSvc;
    private readonly IBackgroundJobClient        _jobs;
    private readonly IWebHostEnvironment         _env;
    private readonly IPayslipDownloadTokenStore  _tokens;

    public PayslipController(
        IPayslipService            payslipSvc,
        IBackgroundJobClient        jobs,
        IWebHostEnvironment         env,
        IPayslipDownloadTokenStore  tokens)
    {
        _payslipSvc = payslipSvc;
        _jobs       = jobs;
        _env        = env;
        _tokens     = tokens;
    }

    // ── IDOR helpers ────────────────────────────────────────────────────────

    private string? CallerEmployeeId =>
        User.FindFirst("employeeId")?.Value;

    private string? CallerRole =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

    private async Task<bool> CallerCanAccessPayslipAsync(int payslipId) =>
        await _payslipSvc.CanAccessPayslipAsync(
            payslipId,
            CallerRole,
            CallerEmployeeId,
            CallerCompanyIdOrNull);

    private string FilePathForToken(string token)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        return Path.Combine(PayslipPdfJob.GetOutputDirectory(webRoot),
                            PayslipPdfJob.GetFileName(token));
    }

    // ── Step 1: Enqueue ─────────────────────────────────────────────────────

    /// <summary>
    /// M-20: Enqueue an async payslip PDF generation job.
    /// Returns 202 Accepted immediately; poll statusUrl until status is "ready",
    /// then fetch the PDF from downloadUrl. The returned token is bound to this
    /// payslip and to the calling user/company, and is valid for a single download.
    /// </summary>
    [HttpPost("{payslipId:int}/pdf")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QueuePdfGeneration(int payslipId)
    {
        if (!await CallerCanAccessPayslipAsync(payslipId))
        {
            // Return NotFound rather than Forbidden to avoid payslip enumeration.
            var meta = await _payslipSvc.GetPayslipMetaAsync(payslipId);
            return meta is null
                ? NotFound(ApiResponse.Fail("Payslip not found."))
                : Forbid();
        }

        // FIX P3-1: token is bound to payslipId + userId + companyId with a 10-minute TTL.
        var token = _tokens.Issue(payslipId, UserId, CallerCompanyIdOrNull);

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        _jobs.Enqueue<PayslipPdfJob>(
            job => job.GenerateAsync(payslipId, token, webRoot));

        var baseUrl     = $"{Request.Scheme}://{Request.Host}";
        var statusUrl   = $"{baseUrl}/api/payslip/{payslipId}/pdf/status/{token}";
        var downloadUrl = $"{baseUrl}/api/payslip/{payslipId}/pdf/download/{token}";

        Response.Headers["Location"] = statusUrl;
        return StatusCode(StatusCodes.Status202Accepted, new
        {
            message            = "PDF generation queued.",
            token,
            statusUrl,
            downloadUrl,
            expiresInSeconds   = (int)PayslipDownloadTokenStore.Ttl.TotalSeconds,
            singleUseDownload  = true
        });
    }

    // ── Step 2: Poll status ─────────────────────────────────────────────────

    /// <summary>Returns the status of a previously queued PDF job.</summary>
    [HttpGet("{payslipId:int}/pdf/status/{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdfStatus(int payslipId, string token)
    {
        if (!await CallerCanAccessPayslipAsync(payslipId))
            return Forbid();

        // Status polling validates the binding without consuming the token.
        if (!_tokens.Validate(token, payslipId, UserId, CallerCompanyIdOrNull))
            return NotFound(ApiResponse.Fail("Unknown or expired download token."));

        var status = System.IO.File.Exists(FilePathForToken(token)) ? "ready" : "processing";
        return Ok(new { status, token, payslipId });
    }

    // ── Step 3: Download ────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the generated PDF when status is "ready".
    /// Returns 404 while the job is still processing, and consumes the token on success.
    /// </summary>
    [HttpGet("{payslipId:int}/pdf/download/{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(int payslipId, string token)
    {
        if (!await CallerCanAccessPayslipAsync(payslipId))
            return Forbid();

        // FIX P3-1: reject tokens issued for a different payslip, user or company, and
        // reject tokens that have already been used or have expired. The check happens
        // before any filesystem access so a replay never touches disk.
        var filePath = FilePathForToken(token);
        if (!System.IO.File.Exists(filePath))
        {
            // Keep the token alive while the job is still running.
            return _tokens.Validate(token, payslipId, UserId, CallerCompanyIdOrNull)
                ? NotFound(ApiResponse.Fail("PDF is not ready yet. Poll the status endpoint."))
                : NotFound(ApiResponse.Fail("Unknown or expired download token."));
        }

        if (!_tokens.ValidateAndConsume(token, payslipId, UserId, CallerCompanyIdOrNull))
            return NotFound(ApiResponse.Fail("Unknown, expired or already-used download token."));

        var meta     = await _payslipSvc.GetPayslipMetaAsync(payslipId);
        var fileName = meta.HasValue
            ? $"payslip-{meta.Value.EmployeeId}-{meta.Value.Year}-{meta.Value.Month:D2}.pdf"
            : $"payslip-{payslipId}.pdf";

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);

        // The token is single-use, so the artefact on disk is no longer reachable —
        // delete it instead of leaving payslip PDFs accumulating under wwwroot.
        try { System.IO.File.Delete(filePath); } catch (IOException) { /* best effort */ }

        return File(bytes, "application/pdf", fileName);
    }
}
