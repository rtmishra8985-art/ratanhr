using System.Security.Claims;
using HRMS.API.Controllers.Employees;
using HRMS.API.Controllers.Payroll;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Cross-tenant IDOR authorization tests.
/// Verifies that a company admin CANNOT access employees from another company
/// across all five affected controllers: Documents, Exit, Promotions, Salary, Bonus.
/// Superadmin must be unrestricted (sees all tenants).
/// </summary>
public class EmployeeAuthorizationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClaimsPrincipal MakePrincipal(string role, int companyId, int userId = 1)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role,           role),
            new Claim("companyId",               companyId.ToString())
        }, "Test"));

    private static Mock<IEmployeeService> EmpSvcNotFound()
    {
        var m = new Mock<IEmployeeService>();
        m.Setup(s => s.GetByIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
         .ReturnsAsync((EmployeeDetailDto?)null);
        return m;
    }

    private static Mock<IEmployeeService> EmpSvcFound()
    {
        var m = new Mock<IEmployeeService>();
        m.Setup(s => s.GetByIdAsync(It.IsAny<string>(), It.IsAny<int?>()))
         .ReturnsAsync(new EmployeeDetailDto { EmployeeId = "EMP9999" });
        return m;
    }

    private static void SetCaller(ControllerBase ctrl, ClaimsPrincipal principal)
    {
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ── EmployeeController (Update / UpdateStatus IDOR) ─────────────────────

    [Fact]
    public async Task Update_CrossTenantAdmin_Gets404()
    {
        // A company-A admin must not be able to overwrite a company-B employee's data.
        var svc = new Mock<IEmployeeService>();
        svc.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<CreateEmployeeDto>(),
                                     It.IsAny<IFormFileCollection>(), It.IsAny<int?>()))
           .ReturnsAsync(false);

        var ctrl = new EmployeeController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));
        ctrl.ControllerContext.HttpContext.Request.ContentType = "multipart/form-data; boundary=----boundary";
        ctrl.ControllerContext.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection());

        var result = await ctrl.Update("EMP_OTHER", new CreateEmployeeDto());
        Assert.IsType<NotFoundObjectResult>(result);

        // Verify companyId: 1 was forwarded — not null (unrestricted).
        svc.Verify(s => s.UpdateAsync("EMP_OTHER", It.IsAny<CreateEmployeeDto>(),
                                       It.IsAny<IFormFileCollection>(), 1), Times.Once);
    }

    [Fact]
    public async Task Update_Superadmin_PassesNullCompanyId()
    {
        // Superadmin must receive null (unrestricted) so it can edit any tenant's employee.
        var svc = new Mock<IEmployeeService>();
        svc.Setup(s => s.UpdateAsync(It.IsAny<string>(), It.IsAny<CreateEmployeeDto>(),
                                     It.IsAny<IFormFileCollection>(), It.IsAny<int?>()))
           .ReturnsAsync(true);

        var ctrl = new EmployeeController(svc.Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));
        ctrl.ControllerContext.HttpContext.Request.ContentType = "multipart/form-data; boundary=----boundary";
        ctrl.ControllerContext.HttpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection());

        var result = await ctrl.Update("EMP_ANY", new CreateEmployeeDto());
        Assert.IsType<OkObjectResult>(result);

        svc.Verify(s => s.UpdateAsync("EMP_ANY", It.IsAny<CreateEmployeeDto>(),
                                       It.IsAny<IFormFileCollection>(), (int?)null), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_CrossTenantAdmin_Gets404()
    {
        // A company-A admin must not be able to deactivate a company-B employee.
        var svc = new Mock<IEmployeeService>();
        svc.Setup(s => s.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>()))
           .ReturnsAsync(false);

        var ctrl = new EmployeeController(svc.Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 2));

        var result = await ctrl.UpdateStatus("EMP_OTHER", new UpdateStatusRequest { IsActive = false });
        Assert.IsType<NotFoundObjectResult>(result);

        svc.Verify(s => s.UpdateStatusAsync("EMP_OTHER", false, 2), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_Superadmin_PassesNullCompanyId()
    {
        var svc = new Mock<IEmployeeService>();
        svc.Setup(s => s.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>()))
           .ReturnsAsync(true);

        var ctrl = new EmployeeController(svc.Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.UpdateStatus("EMP_ANY", new UpdateStatusRequest { IsActive = true });
        Assert.IsType<OkObjectResult>(result);

        svc.Verify(s => s.UpdateStatusAsync("EMP_ANY", true, (int?)null), Times.Once);
    }

    // ── EmployeeDocumentController ──────────────────────────────────────────

    [Fact]
    public async Task Documents_CrossTenantAdmin_Gets404()
    {
        var ctrl = new EmployeeDocumentController(
            new Mock<IEmployeeDocumentService>().Object,
            EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll("EMP_OTHER");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Documents_SameTenantAdmin_Succeeds()
    {
        var docSvc = new Mock<IEmployeeDocumentService>();
        docSvc.Setup(s => s.GetDocumentsAsync(It.IsAny<string>(), It.IsAny<int?>()))
              .ReturnsAsync(new List<EmployeeDocumentDto>());

        var ctrl = new EmployeeDocumentController(docSvc.Object, EmpSvcFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll("EMP_SAME");
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Documents_Superadmin_BypassesTenantCheck()
    {
        var docSvc = new Mock<IEmployeeDocumentService>();
        docSvc.Setup(s => s.GetDocumentsAsync(It.IsAny<string>(), It.IsAny<int?>()))
              .ReturnsAsync(new List<EmployeeDocumentDto>());

        // EmpSvcNotFound — but superadmin skips the check entirely
        var ctrl = new EmployeeDocumentController(docSvc.Object, EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("superadmin", companyId: 0));

        var result = await ctrl.GetAll("EMP_ANY");
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DocumentUpload_CrossTenantAdmin_Gets404()
    {
        var ctrl = new EmployeeDocumentController(
            new Mock<IEmployeeDocumentService>().Object,
            EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Upload("EMP_OTHER", new UploadDocumentDto());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── EmployeeExitController ──────────────────────────────────────────────

    [Fact]
    public async Task Exit_CrossTenantAdmin_Gets404()
    {
        var ctrl = new EmployeeExitController(
            new Mock<IEmployeeExitService>().Object,
            EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Get("EMP_OTHER");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ExitInitiate_CrossTenantAdmin_Gets404()
    {
        var ctrl = new EmployeeExitController(
            new Mock<IEmployeeExitService>().Object,
            EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Initiate("EMP_OTHER", new InitiateExitDto());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── EmployeePromotionController ─────────────────────────────────────────

    [Fact]
    public async Task PromotionGet_CrossTenantAdmin_Gets404()
    {
        var ctrl = new EmployeePromotionController(
            new Mock<IEmployeePromotionService>().Object,
            EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll("EMP_OTHER");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PromotionCreate_CrossTenantAdmin_Gets404()
    {
        var ctrl = new EmployeePromotionController(
            new Mock<IEmployeePromotionService>().Object,
            EmpSvcNotFound().Object);
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Create("EMP_OTHER", new CreatePromotionDto());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── SalaryController ────────────────────────────────────────────────────

    [Fact]
    public async Task Salary_CrossTenantAdmin_Gets404()
    {
        var ctrl = new SalaryController(
            new Mock<ISalaryStructureService>().Object,
            EmpSvcNotFound().Object,
            new Mocks.MockPayrollLockGuard());
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetActive("EMP_OTHER");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SalaryHistory_CrossTenantAdmin_Gets404()
    {
        var ctrl = new SalaryController(
            new Mock<ISalaryStructureService>().Object,
            EmpSvcNotFound().Object,
            new Mocks.MockPayrollLockGuard());
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetHistory("EMP_OTHER");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SalaryUpsert_CrossTenantAdmin_Gets404()
    {
        var ctrl = new SalaryController(
            new Mock<ISalaryStructureService>().Object,
            EmpSvcNotFound().Object,
            new Mocks.MockPayrollLockGuard());
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Upsert("EMP_OTHER", new CreateSalaryStructureDto());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── BonusController ─────────────────────────────────────────────────────

    [Fact]
    public async Task BonusCreate_CrossTenantEmployee_Gets404()
    {
        var ctrl = new BonusController(
            new Mock<IBonusDeductionService>().Object,
            EmpSvcNotFound().Object,
            new Mocks.MockPayrollLockGuard());
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.Create(new CreateBonusDto { EmployeeId = "EMP_OTHER" });
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task BonusGetAll_SpecificCrossTenantEmployee_Gets404()
    {
        var ctrl = new BonusController(
            new Mock<IBonusDeductionService>().Object,
            EmpSvcNotFound().Object,
            new Mocks.MockPayrollLockGuard());
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll("EMP_OTHER", null, null);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task BonusGetAll_NoEmployeeFilter_AllowedForAnyAdmin()
    {
        // Listing all bonuses without an employeeId filter is allowed;
        // company-scoping is enforced at the service layer for broad queries.
        var bonusSvc = new Mock<IBonusDeductionService>();
        bonusSvc.Setup(s => s.GetBonusesAsync(null, It.IsAny<int?>(), null, null))
                .ReturnsAsync(new List<BonusDto>());

        var ctrl = new BonusController(bonusSvc.Object, EmpSvcNotFound().Object, new Mocks.MockPayrollLockGuard());
        SetCaller(ctrl, MakePrincipal("admin", companyId: 1));

        var result = await ctrl.GetAll(null, null, null);
        Assert.IsType<OkObjectResult>(result);
    }
}
