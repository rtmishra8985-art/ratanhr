using HRMS.Domain.Entities.Company;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories;

public interface ICompanyRepository : IGenericRepository<Company>
{
    Task<Company?> GetWithBranchesAsync(int companyId);
    Task<CompanySettings?> GetSettingsAsync(int companyId);
    Task UpsertSettingsAsync(CompanySettings settings);
    Task<List<CompanyBranch>> GetBranchesAsync(int companyId);
    Task<CompanyBranch?> GetBranchByIdAsync(int branchId);
}

public class CompanyRepository : GenericRepository<Company>, ICompanyRepository
{
    public CompanyRepository(ApplicationDbContext ctx) : base(ctx) { }

    public async Task<Company?> GetWithBranchesAsync(int companyId)
        => await _ctx.Companies
            .Include(c => c.Branches)
            .FirstOrDefaultAsync(c => c.Id == companyId);

    public async Task<CompanySettings?> GetSettingsAsync(int companyId)
        => await _ctx.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == companyId);

    public async Task UpsertSettingsAsync(CompanySettings settings)
    {
        var existing = await _ctx.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == settings.CompanyId);
        if (existing == null) await _ctx.CompanySettings.AddAsync(settings);
        else { existing.WorkingDaysPerMonth = settings.WorkingDaysPerMonth; existing.PFPercentage = settings.PFPercentage;
               existing.ESIPercentage = settings.ESIPercentage; existing.PTAmount = settings.PTAmount;
               existing.PayslipFooterNote = settings.PayslipFooterNote; existing.TimeZone = settings.TimeZone;
               existing.CheckInTime = settings.CheckInTime; existing.CheckOutTime = settings.CheckOutTime;
               existing.UpdatedAt = DateTime.UtcNow; }
        await _ctx.SaveChangesAsync();
    }

    public async Task<List<CompanyBranch>> GetBranchesAsync(int companyId)
        => await _ctx.CompanyBranches.Where(b => b.CompanyId == companyId).ToListAsync();

    public async Task<CompanyBranch?> GetBranchByIdAsync(int branchId)
        => await _ctx.CompanyBranches.FirstOrDefaultAsync(b => b.Id == branchId);
}
