using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Employees;

[ApiController]
[Route("api/employees/{employeeId}/documents")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class EmployeeDocumentController : BaseController
{
    private readonly IEmployeeDocumentService _svc;
    private readonly IEmployeeService         _empSvc;

    public EmployeeDocumentController(IEmployeeDocumentService svc, IEmployeeService empSvc)
    { _svc = svc; _empSvc = empSvc; }

    // ── Company-scope guard ─────────────────────────────────────────────────
    // Superadmin sees every tenant; company-admin is restricted to their own company.

    private async Task<bool> EmployeeBelongsToCallerAsync(string employeeId)
    {
        var cid = CallerCompanyIdOrNull;
        if (cid == null) return true;           // superadmin — unrestricted
        return await _empSvc.GetByIdAsync(employeeId, cid) != null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var result = await _svc.GetDocumentsPagedAsync(
            employeeId, page, pageSize, CallerCompanyIdOrNull);
        return Ok(ApiResponse<PagedResult<EmployeeDocumentDto>>.Ok(result));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [EnableRateLimiting("upload")]   // BLOCKER-11: 20 uploads/min per IP
    public async Task<IActionResult> Upload(string employeeId, [FromForm] UploadDocumentDto dto)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var file = Request.Form.Files.GetFile("file");
        // Audit item 9 — MimeValidator replaced by the shared UploadValidator
        // (Document profile: pdf/doc/docx/jpg/jpeg/png, 10 MB, MIME/extension agreement
        // and magic-byte signature). Mismatch → HTTP 400 with the validator's message.
        var upload = HRMS.Infrastructure.Security.UploadValidator.Validate(
            file, HRMS.Infrastructure.Security.UploadProfile.Document);
        if (!upload.IsValid)
            return BadRequest(HRMS.Application.Common.ApiResponse.Fail(upload.Error!));
        dto.EmployeeId = employeeId;
        var id = await _svc.UploadDocumentAsync(
            dto, Request.Form.Files.GetFile("file"), CallerCompanyIdOrNull);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new { Id = id }, "Document uploaded."));
    }

    [HttpPatch("{docId:int}/verify")]
    public async Task<IActionResult> Verify(string employeeId, int docId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var userId = UserId;
        var ok = await _svc.VerifyDocumentAsync(
            docId, userId, employeeId, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Document verified.")) : NotFound(ApiResponse.Fail("Document not found."));
    }

    [HttpDelete("{docId:int}")]
    public async Task<IActionResult> Delete(string employeeId, int docId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));
        var ok = await _svc.DeleteDocumentAsync(
            docId, employeeId, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Document deleted.")) : NotFound(ApiResponse.Fail("Document not found."));
    }

    [HttpGet("{docId:int}/download")]
    public async Task<IActionResult> Download(string employeeId, int docId)
    {
        if (!await EmployeeBelongsToCallerAsync(employeeId))
            return NotFound(ApiResponse.Fail("Employee not found."));

        var (content, fileName) = await _svc.DownloadDocumentAsync(
            docId, employeeId, CallerCompanyIdOrNull);
        return File(content, "application/octet-stream", fileName);
    }
}
