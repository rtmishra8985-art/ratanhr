using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class EmployeePromotionService : IEmployeePromotionService
{
    private readonly ApplicationDbContext _ctx;
    public EmployeePromotionService(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<List<EmployeePromotionDto>> GetPromotionsAsync(string employeeId)
        => await _ctx.EmployeePromotions
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.EffectiveDate)
            .Select(p => new EmployeePromotionDto
            {
                Id              = p.Id,
                EmployeeId      = p.EmployeeId,
                FromDesignation = p.FromDesignation,
                ToDesignation   = p.ToDesignation,
                FromDepartment  = p.FromDepartment,
                ToDepartment    = p.ToDepartment,
                SalaryIncrement = p.SalaryIncrement,
                EffectiveDate   = p.EffectiveDate,
                Reason          = p.Reason,
                Remarks         = p.Remarks,
                CreatedAt       = p.CreatedAt
            }).ToListAsync();

    public async Task<int> CreatePromotionAsync(CreatePromotionDto dto)
    {
        var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == dto.EmployeeId);
        var p = new EmployeePromotion
        {
            EmployeeId      = dto.EmployeeId,
            FromDesignation = dto.FromDesignation ?? emp?.Designation,
            FromDepartment  = dto.FromDepartment  ?? emp?.Department,
            ToDesignation   = dto.ToDesignation,
            ToDepartment    = dto.ToDepartment,
            SalaryIncrement = dto.SalaryIncrement,
            EffectiveDate   = dto.EffectiveDate,
            Reason          = dto.Reason,
            Remarks         = dto.Remarks,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt       = DateTime.UtcNow
        };

        // Apply promotion immediately to the employee record.
        if (emp != null)
        {
            if (!string.IsNullOrEmpty(dto.ToDesignation)) emp.Designation = dto.ToDesignation;
            if (!string.IsNullOrEmpty(dto.ToDepartment))  emp.Department  = dto.ToDepartment;
        }

        _ctx.EmployeePromotions.Add(p);
        await _ctx.SaveChangesAsync();
        return p.Id;
    }

    public async Task<bool> DeletePromotionAsync(int id, int? callerCompanyId = null)
    {
        // FIX IDOR: genuine gap — no ownership check existed before this fix.
        // FindAsync bypasses EF Core global query filters and performed no tenant
        // validation, allowing any admin to delete any promotion by numeric ID.
        // Now a company-scoped JOIN ensures only the owning company's admin can
        // reach the record. SuperAdmin (callerCompanyId == null) is unrestricted.
        EmployeePromotion? p;
        if (callerCompanyId.HasValue)
        {
            p = await (from promo in _ctx.EmployeePromotions
                       join e in _ctx.Employees on promo.EmployeeId equals e.EmployeeCode
                       where promo.Id == id && e.CompanyId == callerCompanyId
                       select promo).FirstOrDefaultAsync();
        }
        else
        {
            p = await _ctx.EmployeePromotions.FirstOrDefaultAsync(x => x.Id == id);
        }
        if (p == null) return false;
        _ctx.EmployeePromotions.Remove(p);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<EmployeePromotionDto>> GetPromotionsPagedAsync(string employeeId, int page, int pageSize)
        => await _ctx.EmployeePromotions
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.EffectiveDate)
            .Select(p => new EmployeePromotionDto { Id = p.Id, EmployeeId = p.EmployeeId,
                FromDesignation = p.FromDesignation, ToDesignation = p.ToDesignation,
                EffectiveDate = p.EffectiveDate, Remarks = p.Remarks,
                CreatedByUserId = p.CreatedByUserId, CreatedAt = p.CreatedAt })
            .ToPagedResultAsync(page, pageSize);
}
