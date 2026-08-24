using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Companies;

[ApiController]
[Route("api/companies/{companyId:int}/branches")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class CompanyBranchController : BaseController
{
    private readonly ICompanyBranchService _svc;

    public CompanyBranchController(ICompanyBranchService svc) => _svc = svc;

    // ── IDOR guard ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the route {companyId} matches the caller's JWT companyId claim.
    /// Superadmins are always allowed (unrestricted cross-tenant access).
    /// Returns false when an admin tries to access another tenant's data (IDOR guard).
    /// </summary>
    private bool CallerOwnsCompany(int companyId)
    {
        if (User.IsInRole(AppRoles.SuperAdmin)) return true;
        return int.TryParse(User.FindFirst("companyId")?.Value, out int cid) && cid == companyId;
    }

    // ── Endpoints ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(int companyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        var result = await _svc.GetBranchesPagedAsync(companyId, page, pageSize);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<CompanyBranchDto>>.Ok(result));
    }

    [HttpGet("{branchId:int}")]
    public async Task<IActionResult> GetById(int companyId, int branchId)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        var branch = await _svc.GetBranchAsync(branchId, companyId);
        if (branch == null) return NotFound(ApiResponse.Fail("Branch not found."));
        return Ok(ApiResponse<CompanyBranchDto>.Ok(branch));
    }

    [HttpPost]
    public async Task<IActionResult> Create(int companyId, [FromBody] CreateCompanyBranchDto dto)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        dto.CompanyId = companyId;
        var id = await _svc.CreateBranchAsync(dto);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { Id = id }, "Branch created successfully."));
    }

    [HttpPut("{branchId:int}")]
    public async Task<IActionResult> Update(int companyId, int branchId, [FromBody] CreateCompanyBranchDto dto)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        dto.CompanyId = companyId;
        var ok = await _svc.UpdateBranchAsync(branchId, companyId, dto);
        return ok ? Ok(ApiResponse.Ok("Branch updated.")) : NotFound(ApiResponse.Fail("Branch not found."));
    }

    [HttpDelete("{branchId:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(int companyId, int branchId)
    {
        // Delete is superadmin-only; role restriction is the primary guard here.
        var ok = await _svc.DeleteBranchAsync(branchId, companyId);
        return ok ? Ok(ApiResponse.Ok("Branch deleted.")) : NotFound(ApiResponse.Fail("Branch not found."));
    }
}
