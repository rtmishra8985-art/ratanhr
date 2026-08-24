using System.Security.Claims;
using HRMS.API.Controllers.Companies;
using HRMS.API.Controllers.Employees;
using HRMS.API.Controllers.Logo;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// IDOR unit tests for the three controllers secured in the latest fix round:
///   • CompanyBranchController  — CallerOwnsCompany() guard (403 on cross-tenant)
///   • EmployeeTransferController — EmployeeBelongsToCallerAsync() guard (404 on cross-tenant)
///   • LogoController           — CallerOwnsCompany() guard (403 on cross-tenant)
/// </summary>
public class IDORNewControllersTests
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

    // ══════════════════════════════════════════════════════════════════════
    // CompanyBranchController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Branch_GetAll_CrossTenantAdmin_Returns403()
    {
        var svc  = new Mock<ICompanyBranchService>();
        var ctrl = new CompanyBranchController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        // Admin from company 1 requests company 2's branches
        var result = await ctrl.GetAll(companyId: 2);

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetBranchesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Branch_GetAll_OwnCompanyAdmin_Returns200()
    {
        var svc = new Mock<ICompanyBranchService>();
        svc.Setup(s => s.GetBranchesAsync(1)).ReturnsAsync(new List<CompanyBranchDto>());
        var ctrl = new CompanyBranchController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll(companyId: 1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Branch_GetAll_Superadmin_CanAccessAnyCompany()
    {
        var svc = new Mock<ICompanyBranchService>();
        svc.Setup(s => s.GetBranchesAsync(99)).ReturnsAsync(new List<CompanyBranchDto>());
        var ctrl = new CompanyBranchController(svc.Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.GetAll(companyId: 99);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Branch_Create_CrossTenantAdmin_Returns403()
    {
        var svc  = new Mock<ICompanyBranchService>();
        var ctrl = new CompanyBranchController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Create(companyId: 2, new CreateCompanyBranchDto());

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.CreateBranchAsync(It.IsAny<CreateCompanyBranchDto>()), Times.Never);
    }

    [Fact]
    public async Task Branch_Update_CrossTenantAdmin_Returns403()
    {
        var svc  = new Mock<ICompanyBranchService>();
        var ctrl = new CompanyBranchController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Update(companyId: 2, branchId: 10, new CreateCompanyBranchDto());

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.UpdateBranchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CreateCompanyBranchDto>()), Times.Never);
    }

    [Fact]
    public async Task Branch_GetById_CrossTenantAdmin_Returns403()
    {
        var svc  = new Mock<ICompanyBranchService>();
        var ctrl = new CompanyBranchController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetById(companyId: 2, branchId: 10);

        Assert.IsType<ForbidResult>(result);
        svc.Verify(s => s.GetBranchAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // EmployeeTransferController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Transfer_GetAll_CrossTenantEmployee_Returns404()
    {
        var transferSvc = new Mock<IEmployeeTransferService>();
        var empSvc      = new Mock<IEmployeeService>();

        // Company 1 admin — employee EMP_CO2 belongs to company 2
        empSvc.Setup(s => s.GetByIdAsync("EMP_CO2", 1))
              .ReturnsAsync((EmployeeDetailDto?)null);

        var ctrl = new EmployeeTransferController(transferSvc.Object, empSvc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll("EMP_CO2");

        Assert.IsType<NotFoundObjectResult>(result);
        transferSvc.Verify(s => s.GetTransfersAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Transfer_GetAll_OwnCompanyEmployee_Returns200()
    {
        var transferSvc = new Mock<IEmployeeTransferService>();
        var empSvc      = new Mock<IEmployeeService>();

        empSvc.Setup(s => s.GetByIdAsync("EMP_CO1", 1))
              .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP_CO1" });
        transferSvc.Setup(s => s.GetTransfersAsync("EMP_CO1"))
                   .ReturnsAsync(new List<EmployeeTransferDto>());

        var ctrl = new EmployeeTransferController(transferSvc.Object, empSvc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll("EMP_CO1");

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Transfer_GetAll_Superadmin_CanAccessAnyEmployee()
    {
        var transferSvc = new Mock<IEmployeeTransferService>();
        var empSvc      = new Mock<IEmployeeService>();

        // Superadmin: GetByIdAsync receives null companyId — returns the employee
        empSvc.Setup(s => s.GetByIdAsync("EMP_ANY", null))
              .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP_ANY" });
        transferSvc.Setup(s => s.GetTransfersAsync("EMP_ANY"))
                   .ReturnsAsync(new List<EmployeeTransferDto>());

        var ctrl = new EmployeeTransferController(transferSvc.Object, empSvc.Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.GetAll("EMP_ANY");

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Transfer_Create_CrossTenantEmployee_Returns404()
    {
        var transferSvc = new Mock<IEmployeeTransferService>();
        var empSvc      = new Mock<IEmployeeService>();

        empSvc.Setup(s => s.GetByIdAsync("EMP_CO2", 1))
              .ReturnsAsync((EmployeeDetailDto?)null);

        var ctrl = new EmployeeTransferController(transferSvc.Object, empSvc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Create("EMP_CO2", new CreateTransferDto());

        Assert.IsType<NotFoundObjectResult>(result);
        transferSvc.Verify(s => s.CreateTransferAsync(It.IsAny<CreateTransferDto>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // LogoController
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Logo_Upload_CrossTenantAdmin_Returns403()
    {
        var companySvc = new Mock<ICompanyService>();
        var ctrl       = new LogoController(companySvc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        // Trying to upload logo for company 2
        var result = await ctrl.Upload(companyId: 2, request: new UploadLogoRequest { Logo = null });

        Assert.IsType<ForbidResult>(result);
        companySvc.Verify(s => s.UpdateLogoAsync(It.IsAny<int>(), It.IsAny<IFormFile>()), Times.Never);
    }

    [Fact]
    public async Task Logo_Upload_OwnCompanyAdmin_NullFile_Returns400()
    {
        var companySvc = new Mock<ICompanyService>();
        var ctrl       = new LogoController(companySvc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        // Own company (1 == 1) but no file supplied
        var result = await ctrl.Upload(companyId: 1, request: new UploadLogoRequest { Logo = null });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Logo_Upload_Superadmin_CanUploadForAnyCompany()
    {
        var companySvc = new Mock<ICompanyService>();
        var mockFile   = new Mock<IFormFile>();
        // The controller validates the file signature against the declared MIME type,
        // so the mock must expose a readable stream with real JPEG magic bytes.
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.FileName).Returns("logo.jpg");
        mockFile.Setup(f => f.Length).Returns(8);
        mockFile.Setup(f => f.OpenReadStream())
                .Returns(() => new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 }));
        companySvc.Setup(s => s.UpdateLogoAsync(99, mockFile.Object)).ReturnsAsync(true);

        var ctrl = new LogoController(companySvc.Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.Upload(companyId: 99, request: new UploadLogoRequest { Logo = mockFile.Object });

        Assert.IsType<OkObjectResult>(result);
    }
}
