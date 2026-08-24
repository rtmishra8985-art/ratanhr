using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class EmployeeTransferService : IEmployeeTransferService
{
    private readonly ApplicationDbContext _ctx;
    public EmployeeTransferService(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<List<EmployeeTransferDto>> GetTransfersAsync(string employeeId)
        => await _ctx.EmployeeTransfers.Where(t => t.EmployeeId == employeeId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new EmployeeTransferDto { Id = t.Id, EmployeeId = t.EmployeeId,
                FromDepartment = t.FromDepartment, ToDepartment = t.ToDepartment,
                FromDesignation = t.FromDesignation, ToDesignation = t.ToDesignation,
                EffectiveDate = t.EffectiveDate, Reason = t.Reason, Status = t.Status,
                CreatedAt = t.CreatedAt }).ToListAsync();

    public async Task<PagedResult<EmployeeTransferDto>> GetTransfersPagedAsync(string employeeId, int page, int pageSize)
    {
        var q = _ctx.EmployeeTransfers.Where(t => t.EmployeeId == employeeId)
            .OrderByDescending(t => t.CreatedAt);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new EmployeeTransferDto { Id = t.Id, EmployeeId = t.EmployeeId,
                FromDepartment = t.FromDepartment, ToDepartment = t.ToDepartment,
                FromDesignation = t.FromDesignation, ToDesignation = t.ToDesignation,
                EffectiveDate = t.EffectiveDate, Reason = t.Reason, Status = t.Status,
                CreatedAt = t.CreatedAt }).ToListAsync();
        return PagedResult<EmployeeTransferDto>.Create(rows, total, page, pageSize);
    }

    public async Task<int> CreateTransferAsync(CreateTransferDto dto)
    {
        var t = new EmployeeTransfer { EmployeeId = dto.EmployeeId,
            FromDepartment = dto.FromDepartment, ToDepartment = dto.ToDepartment,
            FromDesignation = dto.FromDesignation, ToDesignation = dto.ToDesignation,
            FromCompanyId = dto.FromCompanyId, ToCompanyId = dto.ToCompanyId,
            FromBranchId = dto.FromBranchId, ToBranchId = dto.ToBranchId,
            EffectiveDate = dto.EffectiveDate, Reason = dto.Reason,
            Status = "Pending", CreatedAt = DateTime.UtcNow };
        _ctx.EmployeeTransfers.Add(t);
        await _ctx.SaveChangesAsync();
        return t.Id;
    }

    public async Task<bool> ApproveTransferAsync(int transferId, int approvedByUserId, int? companyId = null)
    {
        var t = await _ctx.EmployeeTransfers.FindAsync(transferId);
        if (t == null) return false;
        // Defence-in-depth: verify the transfer's employee belongs to the caller's company.
        // The controller also restricts this action to [Authorize(Roles = AppRoles.SuperAdmin)].
        if (companyId.HasValue)
        {
            var empCheck = await _ctx.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeCode == t.EmployeeId);
            if (empCheck == null || empCheck.CompanyId != companyId) return false;
        }
        t.Status = "Approved"; t.ApprovedByUserId = approvedByUserId;
        // Apply the transfer to the employee record
        var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == t.EmployeeId);
        if (emp != null) {
            if (!string.IsNullOrEmpty(t.ToDepartment)) emp.Department = t.ToDepartment;
            if (!string.IsNullOrEmpty(t.ToDesignation)) emp.Designation = t.ToDesignation;
            // FIX: emp.CompanyId is now non-nullable (int); unwrap the nullable ToCompanyId.
            if (t.ToCompanyId.HasValue) emp.CompanyId = t.ToCompanyId.Value;
        }
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectTransferAsync(int transferId, int? companyId = null)
    {
        var t = await _ctx.EmployeeTransfers.FindAsync(transferId);
        if (t == null) return false;
        // Defence-in-depth: verify the transfer's employee belongs to the caller's company.
        if (companyId.HasValue)
        {
            var emp = await _ctx.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeCode == t.EmployeeId);
            if (emp == null || emp.CompanyId != companyId) return false;
        }
        t.Status = "Rejected";
        await _ctx.SaveChangesAsync();
        return true;
    }
}
