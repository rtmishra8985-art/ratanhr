using HRMS.Application.Common;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetAdminsByCompanyAsync(int? companyId);
}

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext ctx) : base(ctx) { }

    public async Task<User?> GetByEmailAsync(string email)
        => await _set.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

    public async Task<List<User>> GetAdminsByCompanyAsync(int? companyId)
    {
        var q = _set.Where(u => u.Role == AppRoles.Admin);
        if (companyId.HasValue) q = q.Where(u => u.CompanyId == companyId);
        return await q.ToListAsync();
    }
}
