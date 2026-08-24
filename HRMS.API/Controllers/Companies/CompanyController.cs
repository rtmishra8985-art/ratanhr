using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMS.Infrastructure.Security;

namespace HRMS.API.Controllers.Companies;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = "RequireMfaCompleted")]
public class CompanyController : BaseController
{
    private readonly ICompanyService _service;

    public CompanyController(ICompanyService service) => _service = service;

    // ── IDOR helper ────────────────────────────────────────────────────────
    // Returns the caller's companyId claim, or null for superadmin.

    [HttpPost]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var id = await _service.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            ApiResponse<object>.Ok(new { Id = id }, "Company added successfully."));
    }

    /// <summary>
    /// List companies. Superadmin sees all; admin sees only their own company.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        // IDOR fix: a regular admin is scoped to their own company only.
        var companyId = CallerCompanyIdOrNull;
        if (companyId.HasValue)
        {
            // companyId == -1 means the claim was missing — return empty list rather
            // than leaking another company's data.
            if (companyId.Value <= 0)
                return Ok(ApiResponse<List<CompanyDto>>.Ok(new List<CompanyDto>()));

            var single = await _service.GetByIdAsync(companyId.Value);
            var result = single == null
                ? new List<CompanyDto>()
                : new List<CompanyDto> { single };
            return Ok(ApiResponse<List<CompanyDto>>.Ok(result));
        }

        // Superadmin: unrestricted
        var pagedCompanies = await _service.GetAllPagedAsync(page, pageSize);
        return Ok(ApiResponse<HRMS.Application.Common.PagedResult<CompanyDto>>.Ok(pagedCompanies));
    }

    /// <summary>
    /// Get a company by ID. Non-superadmins may only access their own company.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetById(int id)
    {
        // IDOR fix: reject cross-company lookup for non-superadmins.
        var companyId = CallerCompanyIdOrNull;
        if (companyId.HasValue && companyId.Value != id)
            return NotFound(ApiResponse.Fail("Company not found."));

        var company = await _service.GetByIdAsync(id);
        if (company == null)
            return NotFound(ApiResponse.Fail("Company not found."));

        return Ok(ApiResponse<CompanyDto>.Ok(company));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCompanyDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // IDOR fix: non-superadmins may only update their own company.
        var companyId = CallerCompanyIdOrNull;
        if (companyId.HasValue && companyId.Value != id)
            return NotFound(ApiResponse.Fail("Company not found."));

        var updated = await _service.UpdateAsync(id, dto);
        if (!updated)
            return NotFound(ApiResponse.Fail("Company not found."));

        return Ok(ApiResponse.Ok("Company updated successfully."));
    }

    [HttpPost("{id:int}/logo")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(int id, IFormFile logo)
    {
        // Audit item 9 — ad-hoc size/Content-Type/extension checks replaced by the
        // shared UploadValidator (Image profile). This also drops SVG, which was
        // previously accepted and is a stored-XSS vector when served inline.
        // A signature mismatch or spoofed extension now returns HTTP 400 with the
        // reason; CompanyService re-validates on the persistence path.
        var upload = UploadValidator.Validate(logo, UploadProfile.Image);
        if (!upload.IsValid) return BadRequest(ApiResponse.Fail(upload.Error!));

        // IDOR fix: non-superadmins may only upload logo for their own company.
        var companyId = CallerCompanyIdOrNull;
        if (companyId.HasValue && companyId.Value != id)
            return NotFound(ApiResponse.Fail("Company not found."));

        var updated = await _service.UpdateLogoAsync(id, logo);
        if (!updated)
            return NotFound(ApiResponse.Fail("Company not found."));

        return Ok(ApiResponse.Ok("Logo uploaded successfully."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse.Fail("Company not found."));

        return Ok(ApiResponse.Ok("Company deleted successfully."));
    }
}
