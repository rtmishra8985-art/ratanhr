using HRMS.Application.Common;
using HRMS.Domain.Entities;

namespace HRMS.Application.Interfaces;

public interface IPermissionService
{
    Task<Permission?> GetByRoleAsync(string role);
    Task<List<Permission>> GetAllAsync();
    Task<PagedResult<Permission>> GetAllPagedAsync(int page, int pageSize);
    Task<bool> UpsertAsync(Permission permission);
}
