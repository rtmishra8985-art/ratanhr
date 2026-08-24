using System.Security.Claims;
using HRMS.API.Controllers.Leave;
using HRMS.API.Controllers.Payroll;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Phase 1 – D: Extended IDOR tests for Payroll, Leave, and Attendance controllers.
/// Complements the existing EmployeeAuthorizationTests which cover Employee/Document/Exit/Promotion/Salary/Bonus.
/// </summary>
public class IDORExtendedTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ClaimsPrincipal MakePrincipal(string role, int companyId, int userId = 1,
        string? employeeId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role,           role),
            new("companyId",               companyId.ToString()),
        };
        if (employeeId != null) claims.Add(new("employeeId", employeeId));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static void SetCaller(ControllerBase ctrl, ClaimsPrincipal principal)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ── PayrollController.GetById IDOR ────────────────────────────────────

    [Fact]
    public async Task PayrollGetById_CrossTenantAdmin_Gets404()
    {
        // Company-1 admin tries to get a payslip belonging to company-2 employee
        var payrollSvc = new Mock<IPayrollService>();
        payrollSvc.Setup(s => s.GetPayslipAsync(1, 1))
                  .ReturnsAsync((PayslipDto?)null);

        var empSvc = new Mock<IEmployeeService>();
        // Company-1 admin does NOT have this employee
        empSvc.Setup(s => s.GetByIdAsync("EMP_COMPANY2", 1))
              .ReturnsAsync((EmployeeDetailDto?)null);

        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetById(1);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PayrollGetById_OwnCompanyAdmin_Gets200()
    {
        var payrollSvc = new Mock<IPayrollService>();
        payrollSvc.Setup(s => s.GetPayslipAsync(1, 1))
                  .ReturnsAsync(new PayslipDto { EmployeeId = "EMP_COMPANY1" });

        var empSvc = new Mock<IEmployeeService>();
        empSvc.Setup(s => s.GetByIdAsync("EMP_COMPANY1", 1))
              .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP_COMPANY1" });

        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetById(1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task PayrollGetById_Superadmin_Gets200ForAnyTenant()
    {
        // Superadmin should always succeed regardless of company
        var payrollSvc = new Mock<IPayrollService>();
        payrollSvc.Setup(s => s.GetPayslipAsync(1, null))
                  .ReturnsAsync(new PayslipDto { EmployeeId = "EMP_ANY_COMPANY" });

        var empSvc    = new Mock<IEmployeeService>();
        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.GetById(1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task PayrollGetById_Employee_OwnPayslip_Gets200()
    {
        var payrollSvc = new Mock<IPayrollService>();
        payrollSvc.Setup(s => s.GetPayslipAsync(1, 1))
                  .ReturnsAsync(new PayslipDto { EmployeeId = "EMP001" });

        var empSvc    = new Mock<IEmployeeService>();
        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("employee", companyId: 1, employeeId: "EMP001"));

        var result = await ctrl.GetById(1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task PayrollGetById_Employee_OtherEmployeePayslip_Gets404()
    {
        var payrollSvc = new Mock<IPayrollService>();
        payrollSvc.Setup(s => s.GetPayslipAsync(1, 1))
                  .ReturnsAsync(new PayslipDto { EmployeeId = "EMP_OTHER" });

        var empSvc    = new Mock<IEmployeeService>();
        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("employee", companyId: 1, employeeId: "EMP001"));

        var result = await ctrl.GetById(1);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── LeaveController.GetById IDOR ──────────────────────────────────────

    [Fact]
    public async Task LeaveGetById_CrossTenantAdmin_Gets404()
    {
        var leaveSvc = new Mock<ILeaveService>();
        // Leave request belongs to company 2; DB WHERE clause filters it out for company-1 caller
        // FIX HIGH-2: mock now reflects the DB-level tenant filter — service returns null, not 404 post-check
        leaveSvc.Setup(s => s.GetRequestByIdAsync(5, 1))
                .ReturnsAsync((LeaveRequestDto?)null);

        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new LeaveController(leaveSvc.Object, lockGuard);
        // Caller is company 1 admin
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetById(5);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task LeaveGetById_OwnCompanyAdmin_Gets200()
    {
        var leaveSvc = new Mock<ILeaveService>();
        leaveSvc.Setup(s => s.GetRequestByIdAsync(5, 1))
                .ReturnsAsync(new LeaveRequestDto { Id = 5, EmployeeId = "EMP_CO1", CompanyId = 1 });

        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new LeaveController(leaveSvc.Object, lockGuard);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetById(5);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task LeaveGetById_Superadmin_Gets200ForAnyTenant()
    {
        var leaveSvc = new Mock<ILeaveService>();
        leaveSvc.Setup(s => s.GetRequestByIdAsync(5, (int?)null))
                .ReturnsAsync(new LeaveRequestDto { Id = 5, EmployeeId = "EMP_CO_X", CompanyId = 99 });

        var lockGuard = new Mocks.MockPayrollLockGuard();
        var ctrl      = new LeaveController(leaveSvc.Object, lockGuard);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.GetById(5);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── PayrollLock enforcement in PayrollController ───────────────────────

    [Fact]
    public async Task GeneratePayslip_LockedPeriod_Returns409Conflict()
    {
        var payrollSvc = new Mock<IPayrollService>();
        var empSvc     = new Mock<IEmployeeService>();
        var lockGuard  = new Mocks.MockLockedPayrollLockGuard();

        var ctrl = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var dto    = new GeneratePayslipDto { EmployeeId = "EMP001", Month = 7, Year = 2026, WorkingDays = 26, DaysPresent = 22 };
        var result = await ctrl.Generate(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task DeletePayslip_LockedPeriod_Returns409Conflict()
    {
        var payrollSvc = new Mock<IPayrollService>();
        payrollSvc.Setup(s => s.GetPayslipAsync(1, 1))
                  .ReturnsAsync(new PayslipDto { Id = 1, EmployeeId = "EMP001", Month = 7, Year = 2026 });

        var empSvc = new Mock<IEmployeeService>();
        empSvc.Setup(s => s.GetByIdAsync("EMP001", 1))
              .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP001" });

        var lockGuard = new Mocks.MockLockedPayrollLockGuard();
        var ctrl      = new PayrollController(payrollSvc.Object, empSvc.Object, lockGuard, new Mock<IPayrollBulkLockService>().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Delete(1);

        Assert.IsType<ConflictObjectResult>(result);
    }

    // ── LeaveController PayrollLock enforcement ────────────────────────────

    [Fact]
    public async Task LeaveDecide_LockedPeriod_Returns409Conflict()
    {
        var leaveSvc = new Mock<ILeaveService>();
        leaveSvc.Setup(s => s.GetRequestByIdAsync(5, 1))
                .ReturnsAsync(new LeaveRequestDto { Id = 5, StartDate = "2026-07-01", CompanyId = 1 });

        var lockGuard = new Mocks.MockLockedPayrollLockGuard();
        var ctrl      = new LeaveController(leaveSvc.Object, lockGuard);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Decide(5, new LeaveDecisionDto { Approve = true });

        Assert.IsType<ConflictObjectResult>(result);
    }
}
