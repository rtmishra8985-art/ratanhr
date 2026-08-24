using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Companies;

[ApiController]
[Route("api/companies/{companyId:int}/settings")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class CompanySettingsController : BaseController
{
    private readonly ICompanySettingsService _svc;

    public CompanySettingsController(ICompanySettingsService svc) => _svc = svc;

    // IDOR guard: a regular admin must only access settings for their own company.
    // The route exposes {companyId} as a parameter — without this check any admin could
    // read or overwrite another company's settings by supplying a different ID.
    // SuperAdmin is unrestricted and bypasses the ownership check.
    private bool CallerOwnsCompany(int companyId) =>
        User.IsInRole(AppRoles.SuperAdmin) || CompanyId == companyId;

    [HttpGet]
    public async Task<IActionResult> Get(int companyId)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        var s = await _svc.GetSettingsAsync(companyId);
        return Ok(ApiResponse<CompanySettingsDto>.Ok(s!));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(int companyId, [FromBody] UpsertCompanySettingsDto dto)
    {
        if (!CallerOwnsCompany(companyId))
            return Forbid();

        dto.CompanyId = companyId;
        await _svc.UpsertSettingsAsync(dto);
        return Ok(ApiResponse.Ok("Settings saved."));
    }
}
