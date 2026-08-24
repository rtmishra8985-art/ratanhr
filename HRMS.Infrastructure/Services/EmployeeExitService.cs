using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class EmployeeExitService : IEmployeeExitService
{
    private readonly ApplicationDbContext _ctx;
    public EmployeeExitService(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<EmployeeExitDto?> GetExitAsync(string employeeId)
    {
        var x = await _ctx.EmployeeExits.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (x == null) return null;
        return new EmployeeExitDto { Id = x.Id, EmployeeId = x.EmployeeId, ExitType = x.ExitType,
            ResignationDate = x.ResignationDate, LastWorkingDate = x.LastWorkingDate,
            Reason = x.Reason, InterviewNotes = x.InterviewNotes,
            IsNoticePeriodServed = x.IsNoticePeriodServed, GratuityAmount = x.GratuityAmount,
            SettlementAmount = x.SettlementAmount, Status = x.Status, CreatedAt = x.CreatedAt };
    }

    public async Task<int> InitiateExitAsync(InitiateExitDto dto)
    {
        var exit = new EmployeeExit { EmployeeId = dto.EmployeeId, ExitType = dto.ExitType,
            ResignationDate = dto.ResignationDate, LastWorkingDate = dto.LastWorkingDate,
            Reason = dto.Reason, IsNoticePeriodServed = dto.IsNoticePeriodServed,
            Status = "Initiated", InitiatedByUserId = dto.InitiatedByUserId, CreatedAt = DateTime.UtcNow };
        _ctx.EmployeeExits.Add(exit);
        await _ctx.SaveChangesAsync();
        return exit.Id;
    }

    public async Task<bool> CompleteExitAsync(int exitId, CompleteExitDto dto, int? companyId = null)
    {
        // FIX IDOR: replace two-step FindAsync + secondary ownership check with a
        // single company-scoped JOIN query. FindAsync bypasses EF Core global query
        // filters; FirstOrDefaultAsync respects them. SuperAdmin (null) → unrestricted.
        EmployeeExit? x;
        if (companyId.HasValue)
        {
            x = await (from ex in _ctx.EmployeeExits
                       join e in _ctx.Employees on ex.EmployeeId equals e.EmployeeCode
                       where ex.Id == exitId && e.CompanyId == companyId
                       select ex).FirstOrDefaultAsync();
        }
        else
        {
            x = await _ctx.EmployeeExits.FirstOrDefaultAsync(ex => ex.Id == exitId);
        }
        if (x == null) return false;
        x.InterviewNotes = dto.InterviewNotes; x.GratuityAmount = dto.GratuityAmount;
        x.SettlementAmount = dto.SettlementAmount; x.Status = "Completed";
        // Deactivate employee
        var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == x.EmployeeId);
        if (emp != null) emp.IsActive = false;
        await _ctx.SaveChangesAsync();
        return true;
    }
}
