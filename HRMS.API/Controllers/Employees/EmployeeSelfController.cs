using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Employees;

/// <summary>Employee self-service: view and update own profile</summary>
[ApiController]
[Route("api/my")]
[Authorize(Roles = AppRoles.Employee)]
public class EmployeeSelfController : BaseController
{
    private readonly IEmployeeService _service;

    public EmployeeSelfController(IEmployeeService service) => _service = service;

    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();
        // H-01 FIX: scope the lookup to the caller's own company.
        // Without this an employee can craft a request with a manipulated employeeId
        // claim and read another tenant's employee record (IDOR). Passing companyId
        // forces the service WHERE company_id = ? guard — cross-tenant id → 404.
        var companyIdClaim = User.FindFirst("companyId")?.Value;
        int? companyId = int.TryParse(companyIdClaim, out int cid) ? cid : null;

        var emp = await _service.GetByIdAsync(empId, companyId);
        return emp == null ? NotFound(ApiResponse.Fail("Profile not found."))
                           : Ok(ApiResponse<EmployeeDetailDto>.Ok(emp));
    }

    /// <summary>
    /// Employee updates their own profile. Uses <see cref="UpdateSelfProfileDto"/> — a
    /// restricted DTO that omits admin-controlled fields (CompanyId, Designation,
    /// Department, DateOfJoining, etc.) to prevent privilege escalation.
    /// Previously used <c>CreateEmployeeDto</c> which exposed all admin fields.
    /// </summary>
    [HttpPut("profile")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UpdateMyProfile([FromForm] UpdateSelfProfileDto dto)
    {
        var empId = User.FindFirst("employeeId")?.Value;
        if (string.IsNullOrEmpty(empId)) return Unauthorized();

        // Guard: Request.Form.Files throws InvalidOperationException when the request
        // Content-Type is not multipart/form-data (e.g. JSON, missing header, unit-test context).
        if (!Request.HasFormContentType)
            return BadRequest(ApiResponse.Fail("Request must be multipart/form-data."));

        // Map the restricted self-update DTO to the full DTO that the service expects,
        // preserving only the fields an employee is permitted to change.
        var fullDto = new CreateEmployeeDto
        {
            Gender                       = dto.Gender,
            Dob                          = dto.Dob,
            Nationality                  = dto.Nationality,
            MaritalStatus                = dto.MaritalStatus,
            BloodGroup                   = dto.BloodGroup,
            PermanentAddress             = dto.PermanentAddress,
            CurrentAddress               = dto.CurrentAddress,
            EmergencyContactName         = dto.EmergencyContactName,
            EmergencyContactRelationship = dto.EmergencyContactRelationship,
            EmergencyContactPhone        = dto.EmergencyContactPhone,
            EmergencyContactAddress      = dto.EmergencyContactAddress,
            BankAccountHolder            = dto.BankAccountHolder,
            BankName                     = dto.BankName,
            BranchName                   = dto.BranchName,
            AccountNumber                = dto.AccountNumber,
            IfscCode                     = dto.IfscCode,
            Uan                          = dto.Uan,
            Qualification                = dto.Qualification,
            Institution                  = dto.Institution,
            YearOfPassing                = dto.YearOfPassing,
            Specialization               = dto.Specialization,
            PreviousEmployer             = dto.PreviousEmployer,
            JobTitle                     = dto.JobTitle,
            Duration                     = dto.Duration,
            ExpResponsibilities          = dto.ExpResponsibilities,
            Hobbies                      = dto.Hobbies,
            Languages                    = dto.Languages,
            Skills                       = dto.Skills,
            MedicalConditions            = dto.MedicalConditions
            // CompanyId, Designation, Department, Doj intentionally NOT mapped
        };

        var ok = await _service.UpdateAsync(empId, fullDto, Request.Form.Files);
        return ok ? Ok(ApiResponse.Ok("Profile updated."))
                  : NotFound(ApiResponse.Fail("Profile not found."));
    }
}
