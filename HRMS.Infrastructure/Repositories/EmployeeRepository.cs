using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories;

public interface IEmployeeRepository : IGenericRepository<Employee>
{
    Task<Employee?> GetByEmployeeIdAsync(string employeeId);
    Task<List<Employee>> GetByCompanyAsync(int? companyId);
    Task<List<EmployeeDocument>> GetDocumentsAsync(string employeeId);
    Task<List<EmployeeTransfer>> GetTransfersAsync(string employeeId);
    Task<List<EmployeePromotion>> GetPromotionsAsync(string employeeId);
    Task<EmployeeExit?> GetExitAsync(string employeeId);
}

public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(ApplicationDbContext ctx) : base(ctx) { }

    public async Task<Employee?> GetByEmployeeIdAsync(string employeeId)
        => await _set.FirstOrDefaultAsync(e => e.EmployeeCode == employeeId);

    public async Task<List<Employee>> GetByCompanyAsync(int? companyId)
    {
        // FIX O4: hard cap prevents unbounded memory allocation on large tenants.
        // Without this, a company with 10k+ employees causes OOM on the API process.
        // This method is deprecated — callers should migrate to the paged variant.
        // Cap is enforced as a safety guard until all call-sites are migrated.
        const int Cap = 5_000;
        return companyId.HasValue
            ? await _set.Where(e => e.CompanyId == companyId).Take(Cap).ToListAsync()
            : await _set.Take(Cap).ToListAsync();
    }

    public async Task<List<EmployeeDocument>> GetDocumentsAsync(string employeeId)
        => await _ctx.EmployeeDocuments.Where(d => d.EmployeeId == employeeId).ToListAsync();

    public async Task<List<EmployeeTransfer>> GetTransfersAsync(string employeeId)
        => await _ctx.EmployeeTransfers.Where(t => t.EmployeeId == employeeId).OrderByDescending(t => t.CreatedAt).ToListAsync();

    public async Task<List<EmployeePromotion>> GetPromotionsAsync(string employeeId)
        => await _ctx.EmployeePromotions.Where(p => p.EmployeeId == employeeId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<EmployeeExit?> GetExitAsync(string employeeId)
        => await _ctx.EmployeeExits.FirstOrDefaultAsync(x => x.EmployeeId == employeeId);
}
