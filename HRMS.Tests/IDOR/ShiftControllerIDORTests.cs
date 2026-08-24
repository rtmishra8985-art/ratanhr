using System.Security.Claims;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.API.Controllers.Attendance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests.IDOR;

/// <summary>
/// IDOR tests for <see cref="ShiftController.GetAll"/>.
/// Phase 2 requirement: non-SuperAdmin override of a different company must return 403.
/// </summary>
public class ShiftControllerIDORTests
{
    private const int CompanyA = 10;
    private const int CompanyB = 20;

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoOverride_ReturnsOwnCompanyShifts()
    {
        var (controller, svc) = BuildController(companyId: CompanyA, isSuperAdmin: false);
        svc.Setup(s => s.GetShiftsPagedAsync(CompanyA, 1, 25))
           .ReturnsAsync(new PagedResult<ShiftDto>());

        var result = await controller.GetAll(companyIdOverride: null);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify(s => s.GetShiftsPagedAsync(CompanyA, 1, 25), Times.Once);
    }

    [Fact]
    public async Task GetAll_OverrideSameCompany_ReturnsOwnCompanyShifts()
    {
        // Providing own company ID as override is harmless — must be allowed.
        var (controller, svc) = BuildController(companyId: CompanyA, isSuperAdmin: false);
        svc.Setup(s => s.GetShiftsPagedAsync(CompanyA, 1, 25))
           .ReturnsAsync(new PagedResult<ShiftDto>());

        var result = await controller.GetAll(companyIdOverride: CompanyA);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify(s => s.GetShiftsPagedAsync(CompanyA, 1, 25), Times.Once);
    }

    // ── IDOR rejection ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_DifferentCompanyOverride_Returns403()
    {
        var (controller, svc) = BuildController(companyId: CompanyA, isSuperAdmin: false);

        var result = await controller.GetAll(companyIdOverride: CompanyB);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        // Service must NOT be called — no data leak.
        svc.Verify(s => s.GetShiftsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ── Missing company claim ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_MissingCompanyClaim_Returns403()
    {
        // Build a controller with no company claim (simulates a malformed token).
        var (controller, svc) = BuildController(companyId: null, isSuperAdmin: false);

        var result = await controller.GetAll(companyIdOverride: null);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        svc.Verify(s => s.GetShiftsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ── SuperAdmin override ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_SuperAdminOverride_ReturnsTargetCompanyShifts()
    {
        var (controller, svc) = BuildController(companyId: CompanyA, isSuperAdmin: true);
        svc.Setup(s => s.GetShiftsPagedAsync(CompanyB, 1, 25))
           .ReturnsAsync(new PagedResult<ShiftDto>());

        var result = await controller.GetAll(companyIdOverride: CompanyB);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify(s => s.GetShiftsPagedAsync(CompanyB, 1, 25), Times.Once);
    }

    [Fact]
    public async Task GetAll_SuperAdminNoOverride_ReturnsOwnCompanyShifts()
    {
        var (controller, svc) = BuildController(companyId: CompanyA, isSuperAdmin: true);
        svc.Setup(s => s.GetShiftsPagedAsync(CompanyA, 1, 25))
           .ReturnsAsync(new PagedResult<ShiftDto>());

        var result = await controller.GetAll(companyIdOverride: null);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify(s => s.GetShiftsPagedAsync(CompanyA, 1, 25), Times.Once);
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    private static (ShiftController controller, Mock<IShiftService> svc) BuildController(
        int? companyId, bool isSuperAdmin)
    {
        var svc = new Mock<IShiftService>();
        var controller = new ShiftController(svc.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new(ClaimTypes.Name, "testuser"),
        };

        if (companyId.HasValue)
            claims.Add(new Claim("companyId", companyId.Value.ToString()));

        if (isSuperAdmin)
            claims.Add(new Claim(ClaimTypes.Role, AppRoles.SuperAdmin));
        else
            claims.Add(new Claim(ClaimTypes.Role, AppRoles.Admin));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return (controller, svc);
    }
}
