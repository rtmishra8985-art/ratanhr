using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Repositories;

public interface IPayrollRepository
{
    Task<List<Payslip>> GetPayslipsAsync(string? employeeId, int? month, int? year);
    Task<Payslip?> GetPayslipAsync(string employeeId, int month, int year);
    Task<SalaryStructure?> GetActiveSalaryStructureAsync(string employeeId);
    Task<List<SalaryStructure>> GetSalaryHistoryAsync(string employeeId);
    Task<List<Bonus>> GetBonusesAsync(string? employeeId, int? month, int? year);
    Task<List<Deduction>> GetDeductionsAsync(string? employeeId, int? month, int? year);
    Task AddBonusAsync(Bonus bonus);
    Task AddDeductionAsync(Deduction deduction);
    Task UpsertSalaryStructureAsync(SalaryStructure structure);
    Task SaveChangesAsync();
}

public class PayrollRepository : IPayrollRepository
{
    private readonly ApplicationDbContext _ctx;
    public PayrollRepository(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<List<Payslip>> GetPayslipsAsync(string? employeeId, int? month, int? year)
    {
        var q = _ctx.Payslips.AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(p => p.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(p => p.Month == month);
        if (year.HasValue) q = q.Where(p => p.Year == year);
        return await q.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToListAsync();
    }

    public async Task<Payslip?> GetPayslipAsync(string employeeId, int month, int year)
        => await _ctx.Payslips.FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.Month == month && p.Year == year);

    public async Task<SalaryStructure?> GetActiveSalaryStructureAsync(string employeeId)
        => await _ctx.SalaryStructures.Where(s => s.EmployeeId == employeeId && s.IsActive).OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync();

    public async Task<List<SalaryStructure>> GetSalaryHistoryAsync(string employeeId)
        => await _ctx.SalaryStructures.Where(s => s.EmployeeId == employeeId).OrderByDescending(s => s.EffectiveFrom).ToListAsync();

    public async Task<List<Bonus>> GetBonusesAsync(string? employeeId, int? month, int? year)
    {
        var q = _ctx.Bonuses.AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(b => b.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(b => b.Month == month);
        if (year.HasValue) q = q.Where(b => b.Year == year);
        return await q.ToListAsync();
    }

    public async Task<List<Deduction>> GetDeductionsAsync(string? employeeId, int? month, int? year)
    {
        var q = _ctx.Deductions.AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) q = q.Where(d => d.EmployeeId == employeeId);
        if (month.HasValue) q = q.Where(d => d.Month == month);
        if (year.HasValue) q = q.Where(d => d.Year == year);
        return await q.ToListAsync();
    }

    public async Task AddBonusAsync(Bonus bonus) => await _ctx.Bonuses.AddAsync(bonus);
    public async Task AddDeductionAsync(Deduction deduction) => await _ctx.Deductions.AddAsync(deduction);

    public async Task UpsertSalaryStructureAsync(SalaryStructure structure)
    {
        var existing = await _ctx.SalaryStructures.Where(s => s.EmployeeId == structure.EmployeeId && s.IsActive).ToListAsync();
        foreach (var e in existing) e.IsActive = false;
        structure.IsActive = true;
        await _ctx.SalaryStructures.AddAsync(structure);
    }

    public async Task SaveChangesAsync() => await _ctx.SaveChangesAsync();
}
