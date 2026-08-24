using HRMS.Application.DTOs.Department;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

public class DepartmentServiceTests
{
    [Fact]
    public async Task CreateDepartment_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new DepartmentService(db, new MockCacheService());

        var d = await svc.CreateDepartmentAsync(companyId: 1,
            new CreateDepartmentDto { Name = "Engineering", Description = "Tech team" });

        Assert.Equal("Engineering", d.Name);
        Assert.Equal(1, d.CompanyId);
    }

    [Fact]
    public async Task GetDepartments_FiltersGlobalAndCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new DepartmentService(db, new MockCacheService());

        await svc.CreateDepartmentAsync(null, new CreateDepartmentDto { Name = "Global HR" });
        await svc.CreateDepartmentAsync(1,    new CreateDepartmentDto { Name = "Company1 Eng" });
        await svc.CreateDepartmentAsync(2,    new CreateDepartmentDto { Name = "Company2 Sales" });

        var list = await svc.GetDepartmentsAsync(companyId: 1);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task CreateDesignation_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new DepartmentService(db, new MockCacheService());

        var d = await svc.CreateDesignationAsync(1,
            new CreateDesignationDto { Name = "Senior Developer" });

        Assert.Equal("Senior Developer", d.Name);
    }

    [Fact]
    public async Task DeleteDepartment_SoftDeletes()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new DepartmentService(db, new MockCacheService());

        var d = await svc.CreateDepartmentAsync(1, new CreateDepartmentDto { Name = "Old Dept" });
        var ok = await svc.DeleteDepartmentAsync(d.Id, null);
        Assert.True(ok);

        var list = await svc.GetDepartmentsAsync(companyId: 1);
        Assert.Empty(list);
    }
}
