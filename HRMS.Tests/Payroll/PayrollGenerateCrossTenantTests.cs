using System.Security.Claims;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.API.Controllers.Payroll;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests.Payroll;

/// <summary>
/// FIX TEST-01 / FIX BLOCKER-4: Regression tests verifying that a company-A admin
/// cannot generate a payslip for a company-B employee (cross-tenant payroll IDOR).
///
/// FIX BLOCKER-4: Previous version constructed PayrollController with 3 arguments
/// (payrollSvc, lockGuard, empSvc) but the current constructor requires 4 arguments —
/// the fourth being IPayrollBulkLockService (added with FIX HIGH-12). The test
/// therefore did not compile, meaning this critical cross-tenant guard was never
/// actually verified. All constructors now pass all 4 required mocks.
/// </summary>
public class PayrollGenerateCrossTenantTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static DefaultHttpContext MakeAdminContext(int companyId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role,           "Admin"),
            new Claim("companyId",               companyId.ToString()),
        };
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
    }

    private static DefaultHttpContext MakeSuperAdminContext()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role,           AppRoles.SuperAdmin),
        };
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
    }

    /// <summary>
    /// Creates a fully-mocked PayrollController with all 4 constructor dependencies.
    /// FIX BLOCKER-4: Added bulkLock mock — was the missing 4th argument causing
    /// compilation failure.
    /// </summary>
    private static PayrollController MakeController(
        Mock<IPayrollService>         payrollSvc,
        Mock<IPayrollLockGuard>       lockGuard,
        Mock<IEmployeeService>        empSvc,
        Mock<IPayrollBulkLockService>? bulkLock = null)
    {
        bulkLock ??= new Mock<IPayrollBulkLockService>();

        // FIX BLOCKER-4: Pass all 4 required constructor arguments.
        return new PayrollController(
            payrollSvc.Object,
            empSvc.Object,
            lockGuard.Object,
            bulkLock.Object);
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Company-A admin attempts to generate a payslip for company-B employee.
    /// PayslipBelongsToCallerAsync must block the request (primary guard).
    /// The controller must return 404 before reaching the service.
    /// </summary>
    [Fact]
    public async Task Generate_CrossTenant_AdminBlockedAtController()
    {
        // Arrange
        var payrollSvc = new Mock<IPayrollService>();
        var lockGuard  = new Mock<IPayrollLockGuard>();
        var empSvc     = new Mock<IEmployeeService>();
        var bulkLock   = new Mock<IPayrollBulkLockService>();

        lockGuard.Setup(l => l.GetLockMessageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync((string?)null); // period is unlocked

        // Employee EMP-B-001 belongs to company 2, NOT company 1
        empSvc.Setup(e => e.GetByIdAsync("EMP-B-001", 1 /* company 1 */))
              .ReturnsAsync((EmployeeDetailDto?)null); // not found in company 1 → IDOR block

        var ctrl = MakeController(payrollSvc, lockGuard, empSvc, bulkLock);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        var dto = new GeneratePayslipDto
        {
            EmployeeId    = "EMP-B-001",
            Month         = 7,
            Year          = 2026,
            BasicPay      = 50000,
            WorkingDays   = 22,
            DaysPresent   = 22,
            AutoCalculate = true,
        };

        // Act
        var result = await ctrl.Generate(dto);

        // Assert — controller returns 404 (IDOR block)
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound);

        // Service must never be called — cross-tenant block happens at controller level
        payrollSvc.Verify(
            s => s.GeneratePayslipAsync(
                It.IsAny<GeneratePayslipDto>(),
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()),
            Times.Never);
    }

    /// <summary>
    /// Company-A admin generates a payslip for a company-A employee.
    /// Should succeed — same-tenant access is allowed.
    /// </summary>
    [Fact]
    public async Task Generate_SameTenant_AdminAllowed()
    {
        // Arrange
        var payrollSvc = new Mock<IPayrollService>();
        var lockGuard  = new Mock<IPayrollLockGuard>();
        var empSvc     = new Mock<IEmployeeService>();
        var bulkLock   = new Mock<IPayrollBulkLockService>();

        lockGuard.Setup(l => l.GetLockMessageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync((string?)null);

        // Employee EMP-A-001 belongs to company 1 — same tenant
        empSvc.Setup(e => e.GetByIdAsync("EMP-A-001", 1))
              .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP-A-001" });

        payrollSvc.Setup(s => s.GeneratePayslipAsync(
                It.IsAny<GeneratePayslipDto>(), It.IsAny<int?>(), It.IsAny<string?>(), 1))
            .ReturnsAsync(42);

        var ctrl = MakeController(payrollSvc, lockGuard, empSvc, bulkLock);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        var dto = new GeneratePayslipDto
        {
            EmployeeId    = "EMP-A-001",
            Month         = 7,
            Year          = 2026,
            BasicPay      = 50000,
            WorkingDays   = 22,
            DaysPresent   = 22,
            AutoCalculate = true,
        };

        // Act
        var result = await ctrl.Generate(dto);

        // Assert — 201 Created
        var created = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    /// <summary>
    /// Locked payroll period must reject generation regardless of tenant.
    /// </summary>
    [Fact]
    public async Task Generate_LockedPeriod_Returns409()
    {
        // Arrange
        var payrollSvc = new Mock<IPayrollService>();
        var lockGuard  = new Mock<IPayrollLockGuard>();
        var empSvc     = new Mock<IEmployeeService>();
        var bulkLock   = new Mock<IPayrollBulkLockService>();

        lockGuard.Setup(l => l.GetLockMessageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync("Payroll period July 2026 is locked.");

        empSvc.Setup(e => e.GetByIdAsync("EMP-A-001", 1))
              .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP-A-001" });

        var ctrl = MakeController(payrollSvc, lockGuard, empSvc, bulkLock);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        var dto = new GeneratePayslipDto
        {
            EmployeeId    = "EMP-A-001",
            Month         = 7,
            Year          = 2026,
            BasicPay      = 50000,
            WorkingDays   = 22,
            DaysPresent   = 22,
            AutoCalculate = true,
        };

        // Act
        var result = await ctrl.Generate(dto);

        // Assert — 409 Conflict (period locked)
        var conflict = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);

        // Service must not be called
        payrollSvc.Verify(
            s => s.GeneratePayslipAsync(
                It.IsAny<GeneratePayslipDto>(),
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()),
            Times.Never);
    }

    /// <summary>
    /// SuperAdmin calling Generate — CallerCompanyIdOrNull returns null for SuperAdmin,
    /// service receives null (unrestricted cross-tenant access is intentional for SuperAdmin).
    /// </summary>
    [Fact]
    public async Task Generate_SuperAdmin_CrossTenantAllowed()
    {
        // Arrange
        var payrollSvc = new Mock<IPayrollService>();
        var lockGuard  = new Mock<IPayrollLockGuard>();
        var empSvc     = new Mock<IEmployeeService>();
        var bulkLock   = new Mock<IPayrollBulkLockService>();

        // SuperAdmin is never blocked by PayslipBelongsToCallerAsync
        // No lock on the period
        lockGuard.Setup(l => l.GetLockMessageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync((string?)null);

        payrollSvc.Setup(s => s.GeneratePayslipAsync(
                It.IsAny<GeneratePayslipDto>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .ReturnsAsync(99);

        var ctrl = MakeController(payrollSvc, lockGuard, empSvc, bulkLock);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeSuperAdminContext()
        };

        var dto = new GeneratePayslipDto
        {
            EmployeeId    = "EMP-B-001",
            Month         = 7,
            Year          = 2026,
            BasicPay      = 50000,
            WorkingDays   = 22,
            DaysPresent   = 22,
            AutoCalculate = true,
        };

        // Act
        var result = await ctrl.Generate(dto);

        // Assert — 201 Created (SuperAdmin is unrestricted)
        var created = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);

        // Service was called with callerCompanyId = null (SuperAdmin)
        payrollSvc.Verify(
            s => s.GeneratePayslipAsync(It.IsAny<GeneratePayslipDto>(),
                                        It.IsAny<int?>(), It.IsAny<string?>(), null),
            Times.Once);
    }
}
