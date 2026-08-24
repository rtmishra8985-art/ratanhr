using System.Security.Claims;
using HRMS.API.Controllers.Payroll;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using HRMS.Tests.Mocks;

namespace HRMS.Tests;

/// <summary>
/// Security tests for PayrollController.GetAll — IDOR fix.
/// Verifies that a company admin can only retrieve payslips belonging to their own company,
/// and that a SuperAdmin receives payslips from all companies.
/// </summary>
public class PayrollGetAllIdorTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PayrollService BuildPayrollService(HRMS.Infrastructure.Data.ApplicationDbContext db)
    {
        var audit = new Mock<IAuditService>();
        return new PayrollService(db, audit.Object, new MockNotificationService(),
            new MockPayrollCalculator(), new MockLogger<PayrollService>());
    }

    /// <summary>Seeds an employee + payslip for the given company and returns the payslip id.</summary>
    private static async Task<int> SeedPayslipAsync(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        string employeeId,
        int companyId,
        int month = 7, int year = 2026)
    {
        var user = new User
        {
            FullName = $"Employee {employeeId}", Email = $"{employeeId.ToLower()}@test.com",
            PasswordHash = "x", Role = "employee", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Employees.Add(new Employee
        {
            EmployeeCode = employeeId, UserId = user.Id, CompanyId = companyId,
            FullName = $"Employee {employeeId}", Designation = "Dev", Department = "Eng",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });

        var payslip = new Payslip
        {
            EmployeeId = employeeId, Month = month, Year = year,
            BasicPay = 50_000, GrossEarnings = 55_000, NetPay = 50_000,
            CreatedAt = DateTime.UtcNow
        };
        db.Payslips.Add(payslip);
        await db.SaveChangesAsync();
        return payslip.Id;
    }

    private static ClaimsPrincipal MakePrincipal(string role, int companyId, int userId = 1)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name,           "TestUser"),
            new(ClaimTypes.Role,           role),
            new("companyId",               companyId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ClaimsPrincipal MakeSuperAdminPrincipal(int userId = 99)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name,           "SuperAdmin"),
            new(ClaimTypes.Role,           "superadmin"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // ── Tests via service layer (direct, no controller overhead) ─────────────

    [Fact]
    public async Task GetAll_ServiceLayer_SameCompany_ReturnsPayslip()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "EMP_C1_A", companyId: 1);
        var svc = BuildPayrollService(db);

        // Company-1 admin requests payslips for company 1
        var result = await svc.GetAllPayslipsAsync(companyId: 1);

        Assert.Single(result);
        Assert.Equal("EMP_C1_A", result[0].EmployeeId);
    }

    [Fact]
    public async Task GetAll_ServiceLayer_DifferentCompany_ReturnsEmpty()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        // Company-1 has a payslip; company-2 admin requests company-2 payslips
        await SeedPayslipAsync(db, "EMP_C1_B", companyId: 1);
        var svc = BuildPayrollService(db);

        var result = await svc.GetAllPayslipsAsync(companyId: 2);

        Assert.Empty(result); // cross-company payslips must not be returned
    }

    [Fact]
    public async Task GetAll_ServiceLayer_SuperAdmin_ReturnsAllCompanies()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "EMP_C1_C", companyId: 1);
        await SeedPayslipAsync(db, "EMP_C2_A", companyId: 2, month: 8);
        var svc = BuildPayrollService(db);

        // SuperAdmin passes null → unrestricted
        var result = await svc.GetAllPayslipsAsync(companyId: null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAll_ServiceLayer_EmployeeFilter_ScopedToCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "EMP_C1_D", companyId: 1);
        await SeedPayslipAsync(db, "EMP_C2_B", companyId: 2, month: 8);
        var svc = BuildPayrollService(db);

        // Company-1 admin filters by an employee that belongs to company-2 → empty
        var result = await svc.GetAllPayslipsAsync(employeeId: "EMP_C2_B", companyId: 1);

        Assert.Empty(result); // cross-company employee must not be visible
    }

    // ── Controller-level tests ────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Controller_AdminOnlySeesOwnCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "EMP_C1_E", companyId: 1);
        await SeedPayslipAsync(db, "EMP_C3_A", companyId: 3, month: 8);

        var svc      = BuildPayrollService(db);
        var empSvc   = new Mock<IEmployeeService>().Object;
        var guard    = new Mock<IPayrollLockGuard>().Object;
        var bulkLock = new Mock<IPayrollBulkLockService>().Object;
        var ctrl     = new PayrollController(svc, empSvc, guard, bulkLock);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = MakePrincipal("admin", companyId: 1) }
        };

        var result = await ctrl.GetAll(null, null, null) as OkObjectResult;
        Assert.NotNull(result);
        // GetAll now returns a paged envelope.
        var apiResp = result!.Value as ApiResponse<HRMS.Application.Common.PagedResult<PayslipDto>>;
        Assert.NotNull(apiResp);
        Assert.All(apiResp!.Data!.Items, p => Assert.Equal("EMP_C1_E", p.EmployeeId));
    }

    [Fact]
    public async Task GetAll_Controller_SuperAdminSeesAllCompanies()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        await SeedPayslipAsync(db, "EMP_C1_F", companyId: 1);
        await SeedPayslipAsync(db, "EMP_C4_A", companyId: 4, month: 8);

        var svc      = BuildPayrollService(db);
        var empSvc   = new Mock<IEmployeeService>().Object;
        var guard    = new Mock<IPayrollLockGuard>().Object;
        var bulkLock = new Mock<IPayrollBulkLockService>().Object;
        var ctrl     = new PayrollController(svc, empSvc, guard, bulkLock);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = MakeSuperAdminPrincipal() }
        };

        var result = await ctrl.GetAll(null, null, null) as OkObjectResult;
        Assert.NotNull(result);
        var apiResp = result!.Value as ApiResponse<HRMS.Application.Common.PagedResult<PayslipDto>>;
        Assert.NotNull(apiResp);
        Assert.Equal(2, apiResp!.Data!.Items.Count);
    }
}
