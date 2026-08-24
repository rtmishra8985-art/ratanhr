using System.Security.Claims;
using HRMS.API.Controllers.Leave;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using HRMS.Tests.Mocks;

namespace HRMS.Tests;

/// <summary>
/// Security tests for LeaveController.GetAdjustments — IDOR fix.
/// Verifies that a company admin cannot retrieve leave adjustment data
/// for employees belonging to another company.
/// </summary>
public class LeaveAdjustmentIdorTests
{
    private static LeaveService BuildLeaveService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new(db, new Mock<IAuditService>().Object, new Mock<IEmailService>().Object,
               NullLogger<LeaveService>.Instance, new MockNotificationService());

    private static Employee SeedEmployee(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        string employeeId, int companyId)
    {
        var emp = new Employee
        {
            EmployeeCode = employeeId, FullName = $"Employee {employeeId}",
            CompanyId = companyId, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Employees.Add(emp);
        db.SaveChanges();
        return emp;
    }

    private static LeaveType SeedLeaveType(HRMS.Infrastructure.Data.ApplicationDbContext db)
    {
        var lt = new LeaveType { Name = "Casual Leave", AnnualQuotaDays = 10, IsPaid = true, IsActive = true };
        db.LeaveTypes.Add(lt);
        db.SaveChanges();
        return lt;
    }

    // ── Service-layer IDOR tests ──────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceAdjustments_ServiceLayer_SameCompany_ReturnsData()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP001", companyId: 1);
        var lt  = SeedLeaveType(db);
        var svc = BuildLeaveService(db);

        // Create an adjustment for EMP001
        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 1,
            new CreateLeaveBalanceAdjustmentDto
            {
                EmployeeId = "EMP001", LeaveTypeId = lt.Id, Year = 2026, Days = 3, Reason = "Test"
            });

        // Company-1 admin queries EMP001 (same company) → should succeed
        var result = await svc.GetBalanceAdjustmentsAsync("EMP001", year: null, callerCompanyId: 1);

        Assert.NotEmpty(result);
        Assert.All(result, a => Assert.Equal("EMP001", a.EmployeeId));
    }

    [Fact]
    public async Task GetBalanceAdjustments_ServiceLayer_DifferentCompany_ThrowsUnauthorized()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP002", companyId: 1); // employee belongs to company 1
        var lt  = SeedLeaveType(db);
        var svc = BuildLeaveService(db);

        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 1,
            new CreateLeaveBalanceAdjustmentDto
            {
                EmployeeId = "EMP002", LeaveTypeId = lt.Id, Year = 2026, Days = 2, Reason = "Test"
            });

        // Company-2 admin queries EMP002 (belongs to company 1) → must throw
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetBalanceAdjustmentsAsync("EMP002", year: null, callerCompanyId: 2));
    }

    [Fact]
    public async Task GetBalanceAdjustments_ServiceLayer_SuperAdmin_NullCompany_ReturnsData()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP003", companyId: 5);
        var lt  = SeedLeaveType(db);
        var svc = BuildLeaveService(db);

        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 5,
            new CreateLeaveBalanceAdjustmentDto
            {
                EmployeeId = "EMP003", LeaveTypeId = lt.Id, Year = 2026, Days = 4, Reason = "SA test"
            });

        // SuperAdmin (null) → unrestricted
        var result = await svc.GetBalanceAdjustmentsAsync("EMP003", year: null, callerCompanyId: null);

        Assert.NotEmpty(result);
    }

    // ── Controller-level IDOR tests ────────────────────────────────────────────

    private static ClaimsPrincipal MakePrincipal(string role, int companyId, int userId = 1)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role,           role),
            new("companyId",               companyId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ClaimsPrincipal MakeSuperAdminPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "99"),
            new(ClaimTypes.Role,           "superadmin"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task GetAdjustments_Controller_CrossCompany_Returns403()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP_X1", companyId: 10); // belongs to company 10
        var lt  = SeedLeaveType(db);
        var svc = BuildLeaveService(db);

        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 10,
            new CreateLeaveBalanceAdjustmentDto
            {
                EmployeeId = "EMP_X1", LeaveTypeId = lt.Id, Year = 2026, Days = 1, Reason = "Test"
            });

        var guard = new Mock<IPayrollLockGuard>().Object;
        var ctrl  = new LeaveController(svc, guard);
        ctrl.ControllerContext = new ControllerContext
        {
            // Company-20 admin tries to read adjustments for an employee in company-10
            HttpContext = new DefaultHttpContext { User = MakePrincipal("admin", companyId: 20) }
        };

        var result = await ctrl.GetAdjustments("EMP_X1", null);
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetAdjustments_Controller_SameCompany_Returns200()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP_X2", companyId: 7);
        var lt  = SeedLeaveType(db);
        var svc = BuildLeaveService(db);

        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 7,
            new CreateLeaveBalanceAdjustmentDto
            {
                EmployeeId = "EMP_X2", LeaveTypeId = lt.Id, Year = 2026, Days = 2, Reason = "Test"
            });

        var guard = new Mock<IPayrollLockGuard>().Object;
        var ctrl  = new LeaveController(svc, guard);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = MakePrincipal("admin", companyId: 7) }
        };

        var result = await ctrl.GetAdjustments("EMP_X2", null) as OkObjectResult;
        Assert.NotNull(result);
        var apiResp = result!.Value as ApiResponse<List<LeaveBalanceAdjustmentDto>>;
        Assert.NotNull(apiResp);
        Assert.NotEmpty(apiResp!.Data!);
    }

    [Fact]
    public async Task GetAdjustments_Controller_SuperAdmin_Returns200ForAnyCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        SeedEmployee(db, "EMP_X3", companyId: 99);
        var lt  = SeedLeaveType(db);
        var svc = BuildLeaveService(db);

        await svc.CreateBalanceAdjustmentAsync(actorUserId: 1, companyId: 99,
            new CreateLeaveBalanceAdjustmentDto
            {
                EmployeeId = "EMP_X3", LeaveTypeId = lt.Id, Year = 2026, Days = 5, Reason = "SA"
            });

        var guard = new Mock<IPayrollLockGuard>().Object;
        var ctrl  = new LeaveController(svc, guard);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = MakeSuperAdminPrincipal() }
        };

        var result = await ctrl.GetAdjustments("EMP_X3", null) as OkObjectResult;
        Assert.NotNull(result);
    }

    // ── LeaveCarryForward CompanyId fix ────────────────────────────────────────

    [Fact]
    public async Task CarryForward_ServiceLayer_CompanyId_ScopedToCallerCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var lt = SeedLeaveType(db);

        // Company-1 employee with leave balance
        var emp1 = SeedEmployee(db, "EMP_CF1", companyId: 1);
        // Company-2 employee with leave balance
        var emp2 = SeedEmployee(db, "EMP_CF2", companyId: 2);

        var svc = BuildLeaveService(db);

        // Run carry-forward scoped to company 1 only
        var (processed, _) = await svc.CarryForwardBalancesAsync(
            new LeaveCarryForwardDto { FromYear = 2025, ToYear = 2026, CompanyId = 1 },
            actorUserId: 1);

        // Only company-1 employees should be processed; company-2 employees must be untouched
        var adjs = await svc.GetBalanceAdjustmentsAsync("EMP_CF2", year: 2026, callerCompanyId: null);
        Assert.Empty(adjs); // no carry-forward created for company-2 employee
    }
}
