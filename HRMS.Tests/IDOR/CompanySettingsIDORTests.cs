using System.Security.Claims;
using HRMS.API.Controllers.Companies;
using HRMS.Application.DTOs.Company;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests.IDOR;

/// <summary>
/// IDOR unit tests for CompanySettingsController (RBAC-03).
///
/// Verifies that a non-SuperAdmin admin for company A is forbidden from reading
/// or writing settings of company B via the {companyId} route segment.
/// The guard is CallerOwnsCompany() which cross-validates against the JWT claim.
/// </summary>
public class CompanySettingsIDORTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ClaimsPrincipal MakeAdmin(int companyId) =>
        MakePrincipal(AppRoles.Admin, companyId);

    private static ClaimsPrincipal MakeSuperAdmin() =>
        // SuperAdmin carries no companyId claim by design.
        new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, AppRoles.SuperAdmin),
        }, "Test"));

    private static ClaimsPrincipal MakePrincipal(string role, int companyId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Role, role),
            new("companyId", companyId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static CompanySettingsController Wire(
        CompanySettingsController ctrl, ClaimsPrincipal principal)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return ctrl;
    }

    // ── Get ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_CrossTenantAdmin_Returns403()
    {
        // Admin for company-1 tries to read settings of company-2.
        var svc = new Mock<ICompanySettingsService>();
        var ctrl = Wire(new CompanySettingsController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.Get(companyId: 2);

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetSettingsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Get_OwnCompanyAdmin_Returns200()
    {
        var svc = new Mock<ICompanySettingsService>();
        svc.Setup(s => s.GetSettingsAsync(1))
           .ReturnsAsync(new CompanySettingsDto());
        var ctrl = Wire(new CompanySettingsController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.Get(companyId: 1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Get_SuperAdmin_AllowsAnyCompany()
    {
        var svc = new Mock<ICompanySettingsService>();
        svc.Setup(s => s.GetSettingsAsync(42))
           .ReturnsAsync(new CompanySettingsDto());
        var ctrl = Wire(new CompanySettingsController(svc.Object), MakeSuperAdmin());

        var result = await ctrl.Get(companyId: 42);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Upsert ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_CrossTenantAdmin_Returns403()
    {
        // Admin for company-1 tries to overwrite settings of company-2.
        var svc = new Mock<ICompanySettingsService>();
        var ctrl = Wire(new CompanySettingsController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.Upsert(companyId: 2, new UpsertCompanySettingsDto());

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.UpsertSettingsAsync(It.IsAny<UpsertCompanySettingsDto>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_OwnCompanyAdmin_Returns200()
    {
        var svc = new Mock<ICompanySettingsService>();
        svc.Setup(s => s.UpsertSettingsAsync(It.IsAny<UpsertCompanySettingsDto>()))
           .Returns(Task.CompletedTask);
        var ctrl = Wire(new CompanySettingsController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.Upsert(companyId: 1, new UpsertCompanySettingsDto());

        Assert.IsType<OkObjectResult>(result);
    }
}
