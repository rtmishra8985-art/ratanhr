using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.AdminUsers;

[ApiController]
[Route("api/permissions")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class PermissionsController : BaseController
{
    private readonly IPermissionService _service;

    public PermissionsController(IPermissionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await _service.GetAllPagedAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<Permission>>.Ok(result));
    }

    [HttpGet("{role}")]
    [Authorize(Roles = AppRoles.SuperAdminAndAdmin)]
    public async Task<IActionResult> GetByRole(string role)
    {
        var p = await _service.GetByRoleAsync(role);
        return p == null ? NotFound(ApiResponse.Fail("Role not found."))
                         : Ok(ApiResponse<Permission>.Ok(p));
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] Permission permission)
    {
        var ok = await _service.UpsertAsync(permission);
        return Ok(ApiResponse.Ok("Permissions saved."));
    }
}
