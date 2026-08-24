using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;

    public PermissionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Permission?> GetByRoleAsync(string role) =>
        await _db.Permissions.FirstOrDefaultAsync(p => p.Role == role);

    public async Task<List<Permission>> GetAllAsync() =>
        await _db.Permissions.OrderBy(p => p.Role).ToListAsync();

    public async Task<bool> UpsertAsync(Permission permission)
    {
        var existing = await _db.Permissions.FirstOrDefaultAsync(p => p.Role == permission.Role);
        if (existing == null)
        {
            _db.Permissions.Add(permission);
        }
        else
        {
            existing.EmployeeRegistration = permission.EmployeeRegistration;
            existing.ViewAllEmployees = permission.ViewAllEmployees;
            existing.CompanyDetails = permission.CompanyDetails;
            existing.WebAttendanceView = permission.WebAttendanceView;
            existing.ExcelAttendanceUpload = permission.ExcelAttendanceUpload;
            existing.ExcelAttendanceView = permission.ExcelAttendanceView;
            existing.PayrollView = permission.PayrollView;
            existing.PayrollGenerate = permission.PayrollGenerate;
            existing.ReportsAttendance = permission.ReportsAttendance;
            existing.ReportsEmployee = permission.ReportsEmployee;
            existing.Appreciation = permission.Appreciation;
            existing.LogoUpload = permission.LogoUpload;
            existing.ManageAdminUsers = permission.ManageAdminUsers;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<Permission>> GetAllPagedAsync(int page, int pageSize)
        => await _db.Permissions
            .OrderBy(p => p.Role)
            .ToPagedResultAsync(page, pageSize);
}
