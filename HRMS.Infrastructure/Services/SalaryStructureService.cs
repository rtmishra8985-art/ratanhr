using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class SalaryStructureService : ISalaryStructureService
{
    private readonly ApplicationDbContext _ctx;
    public SalaryStructureService(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<SalaryStructureDto?> GetActiveAsync(string employeeId)
    {
        var s = await _ctx.SalaryStructures
            .Where(x => x.EmployeeId == employeeId && x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync();
        return s == null ? null : MapDto(s);
    }

    // FIX MEDIUM: Added pagination to prevent unbounded result sets for long-tenured employees.
    public async Task<List<SalaryStructureDto>> GetHistoryAsync(string employeeId,
        int pageNumber = 1, int pageSize = 25)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize   = Math.Clamp(pageSize, 1, 100);
        // Materialize first — EF Core cannot translate a static mapper method into SQL.
        var rows = await _ctx.SalaryStructures
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveFrom)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return rows.Select(MapDto).ToList();
    }

    public async Task<int> UpsertAsync(CreateSalaryStructureDto dto)
    {
        var existing = await _ctx.SalaryStructures
            .Where(s => s.EmployeeId == dto.EmployeeId && s.IsActive)
            .ToListAsync();
        foreach (var e in existing) { e.IsActive = false; e.EffectiveTo = dto.EffectiveFrom.AddDays(-1); }
        // FIX P1: stamp the owning tenant. The global query filter treats CompanyId == null
        // as "visible to every tenant", so an unstamped salary structure leaks across companies.
        var owner = await _ctx.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e2 => e2.EmployeeCode == dto.EmployeeId)
            ?? throw new KeyNotFoundException($"Employee '{dto.EmployeeId}' not found.");
        var s = new SalaryStructure { EmployeeId = dto.EmployeeId, CompanyId = owner.CompanyId, CTC = dto.CTC,
            BasicPay = dto.BasicPay, HRA = dto.HRA, DA = dto.DA, Conveyance = dto.Conveyance,
            MedicalAllowance = dto.MedicalAllowance, OtherAllowances = dto.OtherAllowances,
            PFEmployee = dto.PFEmployee, PFEmployer = dto.PFEmployer, ESI = dto.ESI, PT = dto.PT,
            TDS = dto.TDS, EffectiveFrom = dto.EffectiveFrom, IsActive = true,
            CreatedByUserId = dto.CreatedByUserId, CreatedAt = DateTime.UtcNow,
            // Tax regime choice — persisted so bulk payroll generation uses the
            // correct regime without re-running the calculator.
            IsOldRegime = dto.IsOldRegime,
            Section80CDeduction = dto.Section80CDeduction };
        _ctx.SalaryStructures.Add(s);
        await _ctx.SaveChangesAsync();
        return s.Id;
    }

    private static SalaryStructureDto MapDto(SalaryStructure s) => new() { Id = s.Id,
        EmployeeId = s.EmployeeId, CTC = s.CTC, BasicPay = s.BasicPay, HRA = s.HRA, DA = s.DA,
        Conveyance = s.Conveyance, MedicalAllowance = s.MedicalAllowance,
        OtherAllowances = s.OtherAllowances, PFEmployee = s.PFEmployee, PFEmployer = s.PFEmployer,
        ESI = s.ESI, PT = s.PT, TDS = s.TDS, EffectiveFrom = s.EffectiveFrom,
        EffectiveTo = s.EffectiveTo, IsActive = s.IsActive, CreatedAt = s.CreatedAt,
        IsOldRegime = s.IsOldRegime, Section80CDeduction = s.Section80CDeduction };
}
