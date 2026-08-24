using HRMS.Application.Common;
using HRMS.Application.DTOs.Onboarding;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Onboarding;

[ApiController]
[Route("api/onboarding")]
[Authorize(Policy = "RequireMfaCompleted")]
public class OnboardingController : BaseController
{
    private readonly IOnboardingService _service;
    public OnboardingController(IOnboardingService service) => _service = service;

    [HttpGet("templates")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> GetTemplates()
    {
        // FIX: use CallerCompanyIdOrNull (null for SuperAdmin = unrestricted scope)
        // instead of the raw CompanyId int property which returns -1 for SuperAdmin.
        var result = await _service.GetTemplatesAsync(CallerCompanyIdOrNull);
        return Ok(ApiResponse<List<OnboardingTemplateDto>>.Ok(result));
    }

    [HttpPost("templates")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateOnboardingTemplateDto dto)
    {
        // FIX: CallerCompanyIdOrNull instead of CompanyId; FIX: 201 Created.
        var result = await _service.CreateTemplateAsync(CallerCompanyIdOrNull, dto);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<OnboardingTemplateDto>.Ok(result, "Template created."));
    }

    [HttpPut("templates/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] CreateOnboardingTemplateDto dto)
    {
        var ok = await _service.UpdateTemplateAsync(id, CallerCompanyIdOrNull, dto);
        return ok ? Ok(ApiResponse.Ok("Updated.")) : NotFound(ApiResponse.Fail("Template not found."));
    }

    [HttpDelete("templates/{id:int}")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var ok = await _service.DeleteTemplateAsync(id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Deleted.")) : NotFound(ApiResponse.Fail("Template not found."));
    }

    [HttpPost("assign")]
    [Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
    public async Task<IActionResult> Assign([FromBody] AssignOnboardingDto dto)
    {
        // FIX: CallerCompanyIdOrNull instead of CompanyId; FIX: 201 Created.
        var result = await _service.AssignAsync(CallerCompanyIdOrNull, dto);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<OnboardingRecordDto>.Ok(result, "Onboarding assigned."));
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRecord()
    {
        var empId = User.FindFirst("employeeId")?.Value ?? "";
        var result = await _service.GetRecordAsync(empId);
        return result != null
            ? Ok(ApiResponse<OnboardingRecordDto>.Ok(result))
            : NotFound(ApiResponse.Fail("No active onboarding record."));
    }

    [HttpPatch("records/{recordId:int}/complete-step")]
    public async Task<IActionResult> MarkStepComplete(int recordId, [FromBody] MarkStepCompleteDto dto)
    {
        var empId = User.FindFirst("employeeId")?.Value ?? "";
        var ok = await _service.MarkStepCompleteAsync(recordId, empId, dto);
        return ok ? Ok(ApiResponse.Ok("Step marked complete."))
                  : NotFound(ApiResponse.Fail("Record not found or access denied."));
    }
}
