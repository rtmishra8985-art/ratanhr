// Fix 2 + Fix 6: Training enrollment security tests — IDOR prevention.
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Training;
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HRMS.Tests.Security;

public class TrainingEnrollmentIdorTests
{
    private static ITrainingService BuildService(HRMS.Infrastructure.Data.ApplicationDbContext db)
        => new TrainingService(db, new Mock<ICacheService>().Object,
            NullLogger<TrainingService>.Instance, new Mock<IAuditService>().Object);

    private static async Task<TrainingProgram> AddProgram(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        int? companyId, bool isActive = true)
    {
        var prog = new TrainingProgram
        {
            Title     = "Test Training",
            CompanyId = companyId,
            IsActive  = isActive,
            MaxSeats  = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.TrainingPrograms.Add(prog);
        await db.SaveChangesAsync();
        return prog;
    }

    private static async Task<Employee> AddEmployee(
        HRMS.Infrastructure.Data.ApplicationDbContext db,
        string empId, int? companyId)
    {
        var emp = new Employee
        {
            EmployeeCode = empId, CompanyId = companyId ?? 0,
            FullName     = empId, IsActive  = true, CreatedAt = DateTime.UtcNow
        };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();
        return emp;
    }

    [Fact]
    public async Task EnrollAsync_SameCompany_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var prog = await AddProgram(db, companyId: 1);
        await AddEmployee(db, "EMP001", companyId: 1);

        var svc = BuildService(db);
        var (ok, _, isCross) = await svc.EnrollAsync(prog.Id, "EMP001");

        Assert.True(ok);
        Assert.False(isCross);
    }

    [Fact]
    public async Task EnrollAsync_CrossCompany_BlockedWithIsCrossTenantFlag()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var prog = await AddProgram(db, companyId: 1); // Company 1 training
        await AddEmployee(db, "EMP002", companyId: 2); // Company 2 employee

        var svc = BuildService(db);
        var (ok, message, isCross) = await svc.EnrollAsync(prog.Id, "EMP002");

        Assert.False(ok);
        Assert.True(isCross, "isCrossTenant flag must be set for controller to return 403");
        Assert.Contains("cross-tenant", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnrollAsync_GlobalTraining_AnyCompanyCanEnroll()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var prog = await AddProgram(db, companyId: null); // global program (no company restriction)
        await AddEmployee(db, "EMP003", companyId: 5);

        var svc = BuildService(db);
        var (ok, _, isCross) = await svc.EnrollAsync(prog.Id, "EMP003");

        Assert.True(ok, "Global programs should accept any company's employees");
        Assert.False(isCross);
    }

    [Fact]
    public async Task EnrollAsync_UnknownEmployee_ReturnsFalseNotCrossTenant()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var prog = await AddProgram(db, companyId: 1);

        var svc = BuildService(db);
        var (ok, message, isCross) = await svc.EnrollAsync(prog.Id, "GHOST999");

        Assert.False(ok);
        Assert.False(isCross); // ghost employee → not found, not a cross-tenant IDOR
        Assert.Contains("not found", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnrollAsync_DoubleEnrollment_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var prog = await AddProgram(db, companyId: 1);
        await AddEmployee(db, "EMP001", companyId: 1);

        var svc = BuildService(db);
        await svc.EnrollAsync(prog.Id, "EMP001"); // first enroll

        var (ok, message, _) = await svc.EnrollAsync(prog.Id, "EMP001"); // second enroll
        Assert.False(ok);
        Assert.Contains("already enrolled", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnrollAsync_InactiveProgram_ReturnsFalse()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var prog = await AddProgram(db, companyId: 1, isActive: false);
        await AddEmployee(db, "EMP001", companyId: 1);

        var svc = BuildService(db);
        var (ok, _, _) = await svc.EnrollAsync(prog.Id, "EMP001");

        Assert.False(ok);
    }
}
