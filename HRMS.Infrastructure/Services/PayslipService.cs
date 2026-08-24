using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// FIX HIGH-PS5: Service that encapsulates all payslip DB access so controllers
/// no longer need to inject ApplicationDbContext directly.
/// </summary>
public class PayslipService : IPayslipService
{
    private readonly ApplicationDbContext _db;

    public PayslipService(ApplicationDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<bool> CanAccessPayslipAsync(
        int     payslipId,
        string? callerRole,
        string? callerEmployeeId,
        int?    callerCompanyId)
    {
        var payslip = await _db.Payslips.FirstOrDefaultAsync(x => x.Id == payslipId);
        if (payslip is null) return false;

        // Employees may only access their own payslips.
        if (callerRole == AppRoles.Employee)
            return callerEmployeeId == payslip.EmployeeId;

        // Admins are scoped to their own company.
        if (callerRole == AppRoles.Admin)
        {
            if (callerCompanyId is null) return false;
            var emp = await _db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeCode == payslip.EmployeeId);
            return emp?.CompanyId == callerCompanyId;
        }

        // Superadmin (and any other privileged role): unrestricted.
        return true;
    }

    /// <inheritdoc/>
    public async Task<(string EmployeeId, int Month, int Year)?> GetPayslipMetaAsync(int payslipId)
    {
        var payslip = await _db.Payslips.FirstOrDefaultAsync(x => x.Id == payslipId);
        if (payslip is null) return null;
        return (payslip.EmployeeId, payslip.Month, payslip.Year);
    }
}
