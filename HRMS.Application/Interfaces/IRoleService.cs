using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;

namespace HRMS.Application.Interfaces;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllRolesAsync();
    Task<PagedResult<RoleDto>> GetAllRolesPagedAsync(int page, int pageSize);
    Task<int> CreateRoleAsync(CreateRoleDto dto);
    Task<bool> UpdateRoleAsync(int id, CreateRoleDto dto);
    Task<bool> DeleteRoleAsync(int id);
}
