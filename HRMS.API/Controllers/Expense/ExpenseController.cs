using HRMS.Application.Common;
using HRMS.Application.DTOs.Expense;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMS.Infrastructure.Security;

namespace HRMS.API.Controllers.Expense;

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = "RequireMfaCompleted")]
public class ExpenseController : BaseController
{
    private readonly IExpenseService _service;
    public ExpenseController(IExpenseService service) => _service = service;

    // ── IDOR guard ─────────────────────────────────────────────────────────
    // FIX: Shadow BaseController.CompanyId (int, returns -1 for SuperAdmin) with
    // an int? version that returns null for SuperAdmin and the JWT claim value for
    // all other roles.  Service methods that receive null skip the tenant filter
    // (SuperAdmin cross-company view); a non-SuperAdmin whose claim is absent or
    // malformed gets null → service returns nothing rather than leaking company 0.
    // This closes cross-tenant data leaks on the three admin read endpoints below:
    //   GET /api/expenses/dashboard
    //   GET /api/expenses
    //   GET /api/expenses/report
    private new int? CompanyId =>
        User.IsInRole(AppRoles.SuperAdmin) ? (int?)null
        : int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : null;

    // ── Dashboard ──────────────────────────────────────────────────────────

    /// <summary>Expense dashboard stats and charts for the current company.</summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Dashboard()
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetDashboardAsync(CompanyId);
        return Ok(ApiResponse<ExpenseDashboardDto>.Ok(result));
    }

    // ── Admin: list + reports ──────────────────────────────────────────────

    /// <summary>Admin: paginated list of all expense claims.</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null)
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetAllAsync(CompanyId, page, pageSize, status);
        return Ok(ApiResponse<PagedResult<ExpenseDto>>.Ok(result));
    }

    /// <summary>Admin: filtered report.</summary>
    [HttpGet("report")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Report([FromQuery] ExpenseReportFilterDto filter)
    {
        // FIX (unscoped→scoped): was CompanyId (int, -1 for SuperAdmin).
        var result = await _service.GetReportAsync(CompanyId, filter);
        return Ok(ApiResponse<PagedResult<ExpenseDto>>.Ok(result));
    }

    // ── Employee: own claims ───────────────────────────────────────────────

    /// <summary>Employee: list own expense claims.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var empId = EmployeeIdStr;
        var result = await _service.GetMyClaimsAsync(empId);
        return Ok(ApiResponse<List<ExpenseDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id, CompanyId);
        return result != null
            ? Ok(ApiResponse<ExpenseDto>.Ok(result))
            : NotFound(ApiResponse.Fail("Expense claim not found."));
    }

    /// <summary>Employee: create a new expense claim in Draft state with line items.</summary>
    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateExpenseClaimDto dto)
    {
        // Audit item 9 — every line-item receipt is validated with the Document
        // profile (.pdf/.doc/.docx/.jpg/.jpeg/.png) before the claim is persisted.
        // Receipts are optional, so a missing file is not an error.
        foreach (var item in dto.Items ?? new List<CreateExpenseItemDto>())
        {
            var receipt = UploadValidator.Validate(item.Receipt, UploadProfile.Document, required: false);
            if (!receipt.IsValid) return BadRequest(ApiResponse.Fail(receipt.Error!));
        }

        var empId = EmployeeIdStr;
        var result = await _service.CreateDraftAsync(empId, CompanyId, dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<ExpenseDto>.Ok(result, "Expense claim created as Draft."));
    }

    /// <summary>Employee: submit a Draft claim for Manager approval.</summary>
    [HttpPatch("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        var empId = EmployeeIdStr;
        var ok = await _service.SubmitAsync(id, empId);
        return ok ? Ok(ApiResponse.Ok("Submitted for Manager approval."))
                  : BadRequest(ApiResponse.Fail("Cannot submit — claim not found or not in Draft state."));
    }

    /// <summary>Approver (Manager / Finance): approve, reject, or send back.</summary>
    [HttpPatch("{id:int}/decide")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Decide(int id, [FromBody] ExpenseDecisionDto dto)
    {
        var reviewerName = UserId.ToString();
        var ok = await _service.DecideAsync(id, UserId, reviewerName, CompanyId, dto);
        if (!ok)
            return NotFound(ApiResponse.Fail("Claim not found, or no pending approval for this step."));
        var msg = dto.SendBack ? "Sent back." : (dto.Approve ? $"Approved by {dto.Step}." : $"Rejected by {dto.Step}.");
        return Ok(ApiResponse.Ok(msg));
    }

    /// <summary>Employee: soft-delete a Draft claim.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var empId = EmployeeIdStr;
        var ok = await _service.DeleteAsync(id, empId);
        return ok ? Ok(ApiResponse.Ok("Deleted."))
                  : NotFound(ApiResponse.Fail("Claim not found or not in Draft state."));
    }

    // ── Legacy endpoint (backward-compat) ────────────────────────────────

    /// <summary>
    /// Legacy single-item submission (multipart/form-data).
    /// Maintained for backward compatibility with any existing integrations.
    /// </summary>
    [HttpPost("legacy")]
    [Obsolete]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> SubmitLegacy([FromForm] CreateExpenseDto dto)
    {
        // Audit item 9 — legacy receipt goes through the same Document profile.
        var legacyReceipt = UploadValidator.Validate(dto.Receipt, UploadProfile.Document, required: false);
        if (!legacyReceipt.IsValid) return BadRequest(ApiResponse.Fail(legacyReceipt.Error!));

        var empId = EmployeeIdStr;
#pragma warning disable CS0618
        var result = await _service.SubmitLegacyAsync(empId, CompanyId, dto);
#pragma warning restore CS0618
        return Ok(ApiResponse<ExpenseDto>.Ok(result, "Expense submitted (legacy)."));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private string EmployeeIdStr => User.FindFirst("employeeId")?.Value ?? string.Empty;
}
