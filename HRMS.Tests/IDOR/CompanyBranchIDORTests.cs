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
/// IDOR unit tests for CompanyBranchController (RBAC-02).
///
/// Every test asserts that a non-SuperAdmin admin for company A CANNOT access
/// or mutate branch data belonging to company B by supplying a different
/// {companyId} route segment. The IDOR guard is the CallerOwnsCompany() helper
/// on the controller which validates the route companyId against the JWT claim.
/// </summary>
public class CompanyBranchIDORTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ClaimsPrincipal MakeAdmin(int companyId) =>
        MakePrincipal(AppRoles.Admin, companyId);

    private static ClaimsPrincipal MakeSuperAdmin() =>
        MakePrincipal(AppRoles.SuperAdmin, companyId: 0);

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

    private static CompanyBranchController Wire(
        CompanyBranchController ctrl, ClaimsPrincipal principal)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return ctrl;
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_CrossTenantAdmin_Returns403()
    {
        // Company-1 admin tries to list branches of company-2.
        var svc = new Mock<ICompanyBranchService>();
        var ctrl = Wire(new CompanyBranchController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.GetAll(companyId: 2);

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetBranchesPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_OwnCompanyAdmin_Returns200()
    {
        var svc = new Mock<ICompanyBranchService>();
        svc.Setup(s => s.GetBranchesPagedAsync(1, 1, 25))
           .ReturnsAsync(new HRMS.Application.Common.PagedResult<CompanyBranchDto>
           {
               Items = new List<CompanyBranchDto>(),
               TotalCount = 0,
               Page = 1,
               PageSize = 25
           });
        var ctrl = Wire(new CompanyBranchController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.GetAll(companyId: 1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_SuperAdmin_AllowsAnyCompany()
    {
        var svc = new Mock<ICompanyBranchService>();
        svc.Setup(s => s.GetBranchesPagedAsync(99, 1, 25))
           .ReturnsAsync(new HRMS.Application.Common.PagedResult<CompanyBranchDto>
           {
               Items = new List<CompanyBranchDto>(),
               TotalCount = 0,
               Page = 1,
               PageSize = 25
           });
        var ctrl = Wire(new CompanyBranchController(svc.Object), MakeSuperAdmin());

        var result = await ctrl.GetAll(companyId: 99);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_CrossTenantAdmin_Returns403()
    {
        var svc = new Mock<ICompanyBranchService>();
        var ctrl = Wire(new CompanyBranchController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.GetById(companyId: 2, branchId: 10);

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetBranchAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_CrossTenantAdmin_Returns403()
    {
        var svc = new Mock<ICompanyBranchService>();
        var ctrl = Wire(new CompanyBranchController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.Create(companyId: 2, new CreateCompanyBranchDto());

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.CreateBranchAsync(It.IsAny<CreateCompanyBranchDto>()), Times.Never);
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_CrossTenantAdmin_Returns403()
    {
        var svc = new Mock<ICompanyBranchService>();
        var ctrl = Wire(new CompanyBranchController(svc.Object), MakeAdmin(companyId: 1));

        var result = await ctrl.Update(companyId: 2, branchId: 5, new CreateCompanyBranchDto());

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.UpdateBranchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CreateCompanyBranchDto>()), Times.Never);
    }
}
