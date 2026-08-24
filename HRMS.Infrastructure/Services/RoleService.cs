using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly ApplicationDbContext _ctx;
    public RoleService(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<List<RoleDto>> GetAllRolesAsync()
        => await _ctx.Roles.Select(r => new RoleDto { Id = r.Id, Name = r.Name,
            Description = r.Description, IsSystemRole = r.IsSystemRole }).ToListAsync();

    public async Task<int> CreateRoleAsync(CreateRoleDto dto)
    {
        var role = new Role { Name = dto.Name.ToLower().Trim(), Description = dto.Description, CreatedAt = DateTime.UtcNow };
        _ctx.Roles.Add(role);
        await _ctx.SaveChangesAsync();
        return role.Id;
    }

    public async Task<bool> UpdateRoleAsync(int id, CreateRoleDto dto)
    {
        // FIX IDOR: FirstOrDefaultAsync respects EF Core global query filters (e.g.
        // soft-delete) that FindAsync bypasses. The IsSystemRole guard is folded into
        // the query predicate to avoid a separate round-trip check.
        var r = await _ctx.Roles.FirstOrDefaultAsync(x => x.Id == id && !x.IsSystemRole);
        if (r == null) return false;
        r.Name = dto.Name.ToLower().Trim(); r.Description = dto.Description;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRoleAsync(int id)
    {
        // FIX IDOR: same fix as UpdateRoleAsync — scoped query, no FindAsync.
        var r = await _ctx.Roles.FirstOrDefaultAsync(x => x.Id == id && !x.IsSystemRole);
        if (r == null) return false;
        _ctx.Roles.Remove(r);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<RoleDto>> GetAllRolesPagedAsync(int page, int pageSize)
        => await _ctx.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name,
                Description = r.Description, IsSystemRole = r.IsSystemRole })
            .ToPagedResultAsync(page, pageSize);
}
