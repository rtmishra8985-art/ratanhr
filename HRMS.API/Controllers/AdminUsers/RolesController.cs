using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.AdminUsers;

[ApiController]
[Route("api/roles")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class RolesController : BaseController
{
    private readonly IRoleService _svc;

    public RolesController(IRoleService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await _svc.GetAllRolesPagedAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<RoleDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        var id = await _svc.CreateRoleAsync(dto);
        // FIX: HTTP 201 Created for resource creation (was 200 OK).
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<object>.Ok(new { Id = id }, "Role created."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateRoleDto dto)
    {
        var ok = await _svc.UpdateRoleAsync(id, dto);
        return ok ? Ok(ApiResponse.Ok("Role updated.")) : NotFound(ApiResponse.Fail("Role not found."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteRoleAsync(id);
        return ok ? Ok(ApiResponse.Ok("Role deleted.")) : BadRequest(ApiResponse.Fail("Cannot delete system role."));
    }
}
