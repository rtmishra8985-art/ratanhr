using System.Security.Claims;
using HRMS.API.Controllers.Payroll;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// Security tests for bonus and deduction endpoints.
///
/// Verifies that:
///   1. Company-A admins cannot create bonuses/deductions for Company-B employees (IDOR).
///   2. SuperAdmin can access across tenants.
///   3. Payroll-lock prevents writes on locked periods.
/// </summary>
public class BonusDeductionSecurityTests
{
    // ── Test helpers ──────────────────────────────────────────────────────

    private static DefaultHttpContext MakeAdminContext(int companyId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role,           AppRoles.Admin),
            new Claim("companyId",               companyId.ToString()),
        };
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
    }

    private static DefaultHttpContext MakeSuperAdminContext()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role,           AppRoles.SuperAdmin),
        };
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
    }

    // ══════════════════════════════════════════════════════════════════════
    // Bonus IDOR
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBonus_AdminForDifferentCompany_ReturnsNotFound()
    {
        // Arrange
        var empSvcMock = new Mock<IEmployeeService>();
        // Employee belongs to company 2; admin is for company 1 → cross-tenant → null result
        empSvcMock
            .Setup(s => s.GetByIdAsync("EMP-002", 1))
            .ReturnsAsync((EmployeeDetailDto?)null);

        var bonusSvcMock  = new Mock<IBonusDeductionService>();
        var lockGuardMock = new Mock<IPayrollLockGuard>();

        var ctrl = new BonusController(
            bonusSvcMock.Object, empSvcMock.Object, lockGuardMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        // Act — EmployeeId is inside the DTO, not a route param
        var result = await ctrl.Create(new CreateBonusDto
        {
            EmployeeId  = "EMP-002",
            Amount      = 5000m,
            Month       = 7,
            Year        = 2026,
            Remarks = "Performance bonus"
        });

        // Assert: 404 because employee is not in the caller's tenant.
        Assert.IsType<NotFoundObjectResult>(result);
        bonusSvcMock.Verify(s => s.AddBonusAsync(It.IsAny<CreateBonusDto>()), Times.Never,
            "BonusDeductionService.AddBonusAsync must NOT be called when IDOR check fails.");
    }

    [Fact]
    public async Task CreateBonus_AdminForSameCompany_CallsService()
    {
        var emp = new EmployeeDetailDto { EmployeeId = "EMP-001" };

        var empSvcMock = new Mock<IEmployeeService>();
        empSvcMock.Setup(s => s.GetByIdAsync("EMP-001", 1)).ReturnsAsync(emp);

        var bonusSvcMock = new Mock<IBonusDeductionService>();
        bonusSvcMock.Setup(s => s.AddBonusAsync(It.IsAny<CreateBonusDto>())).ReturnsAsync(42);

        var lockGuardMock = new Mock<IPayrollLockGuard>();
        lockGuardMock.Setup(s => s.GetLockMessageAsync(1, 7, 2026)).ReturnsAsync((string?)null);

        var ctrl = new BonusController(
            bonusSvcMock.Object, empSvcMock.Object, lockGuardMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        var result = await ctrl.Create(new CreateBonusDto
        {
            EmployeeId  = "EMP-001",
            Amount      = 5000m,
            Month       = 7,
            Year        = 2026,
            Remarks = "Performance bonus"
        });

        var created = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(201, created.StatusCode); // resource creation returns 201 Created
        bonusSvcMock.Verify(s => s.AddBonusAsync(It.IsAny<CreateBonusDto>()), Times.Once);
    }

    [Fact]
    public async Task CreateBonus_LockedPeriod_ReturnsConflict()
    {
        var emp = new EmployeeDetailDto { EmployeeId = "EMP-001" };

        var empSvcMock = new Mock<IEmployeeService>();
        empSvcMock.Setup(s => s.GetByIdAsync("EMP-001", 1)).ReturnsAsync(emp);

        var bonusSvcMock = new Mock<IBonusDeductionService>();

        var lockGuardMock = new Mock<IPayrollLockGuard>();
        lockGuardMock
            .Setup(s => s.GetLockMessageAsync(1, 7, 2026))
            .ReturnsAsync("Payroll period Jul 2026 is locked.");

        var ctrl = new BonusController(
            bonusSvcMock.Object, empSvcMock.Object, lockGuardMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        var result = await ctrl.Create(new CreateBonusDto
        {
            EmployeeId  = "EMP-001",
            Amount      = 3000m,
            Month       = 7,
            Year        = 2026,
            Remarks = "Bonus attempt on locked period"
        });

        Assert.IsType<ConflictObjectResult>(result);
        bonusSvcMock.Verify(s => s.AddBonusAsync(It.IsAny<CreateBonusDto>()), Times.Never,
            "AddBonusAsync must NOT be called when period is locked.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Deduction IDOR
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDeduction_AdminForDifferentCompany_ReturnsNotFound()
    {
        var empSvcMock = new Mock<IEmployeeService>();
        empSvcMock
            .Setup(s => s.GetByIdAsync("EMP-003", 1))
            .ReturnsAsync((EmployeeDetailDto?)null);

        var dedSvcMock    = new Mock<IBonusDeductionService>();
        var lockGuardMock = new Mock<IPayrollLockGuard>();

        var ctrl = new DeductionController(
            dedSvcMock.Object, empSvcMock.Object, lockGuardMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeAdminContext(companyId: 1)
        };

        var result = await ctrl.Create(new CreateDeductionDto
        {
            EmployeeId  = "EMP-003",
            Amount      = 1000m,
            Month       = 7,
            Year        = 2026,
            Remarks = "Loan"
        });

        Assert.IsType<NotFoundObjectResult>(result);
        dedSvcMock.Verify(s => s.AddDeductionAsync(It.IsAny<CreateDeductionDto>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // SuperAdmin cross-tenant access
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBonus_SuperAdmin_CanAccessAnyCompany()
    {
        // SuperAdmin has CallerCompanyIdOrNull = null → EmployeeBelongsToCallerAsync returns true.
        var empSvcMock = new Mock<IEmployeeService>();
        empSvcMock.Setup(s => s.GetByIdAsync("EMP-099", null)).ReturnsAsync(
            new EmployeeDetailDto { EmployeeId = "EMP-099" });

        var bonusSvcMock = new Mock<IBonusDeductionService>();
        bonusSvcMock.Setup(s => s.AddBonusAsync(It.IsAny<CreateBonusDto>())).ReturnsAsync(1);

        var lockGuardMock = new Mock<IPayrollLockGuard>();
        lockGuardMock
            .Setup(s => s.GetLockMessageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string?)null);

        var ctrl = new BonusController(
            bonusSvcMock.Object, empSvcMock.Object, lockGuardMock.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = MakeSuperAdminContext()
        };

        var result = await ctrl.Create(new CreateBonusDto
        {
            EmployeeId  = "EMP-099",
            Amount      = 10000m,
            Month       = 7,
            Year        = 2026,
            Remarks = "Year-end bonus"
        });

        var created = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(201, created.StatusCode); // resource creation returns 201 Created
    }
}
