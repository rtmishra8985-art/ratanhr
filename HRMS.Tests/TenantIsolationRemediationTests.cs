// Regression tests for the 2026-08-12 production-audit remediation.
//   AUDIT-07S-01  cross-tenant write via POST /api/onboarding/assign
//   AUDIT-07S-02  company admin deleting a GLOBAL webhook subscription
//   AUDIT-07S-03  PayrollLockGuard.GetLocksAsync int/int? compare hides locks from SuperAdmin
using HRMS.Application.DTOs.Onboarding;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Onboarding;
using HRMS.Domain.Entities.Webhook;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Threading.Channels;
using Xunit;

namespace HRMS.Tests;

public class OnboardingAssignTenantIsolationTests
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;

    private static OnboardingService Build(ApplicationDbContext db)
        => new OnboardingService(db, NullLogger<OnboardingService>.Instance);

    /// <summary>Seeds one template + one employee for company A and for company B.</summary>
    private static (int tplA, int tplB) Seed(ApplicationDbContext db)
    {
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP-A1", CompanyId = CompanyA, FullName = "Alice A"
        });
        db.Employees.Add(new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP-B1", CompanyId = CompanyB, FullName = "Bob B"
        });
        var tplA = new OnboardingTemplate
        {
            CompanyId = CompanyA, Name = "A-Template",
            Steps = """[{"title":"Step 1"}]""", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var tplB = new OnboardingTemplate
        {
            CompanyId = CompanyB, Name = "B-Template",
            Steps = """[{"title":"Step 1"}]""", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.OnboardingTemplates.AddRange(tplA, tplB);
        db.SaveChanges();
        return (tplA.Id, tplB.Id);
    }

    [Fact]
    public async Task Assign_SameTenant_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (tplA, _) = Seed(db);

        var result = await Build(db).AssignAsync(CompanyA, new AssignOnboardingDto
        {
            EmployeeId = "EMP-A1", TemplateId = tplA, DueDate = DateTime.UtcNow.AddDays(30)
        });

        Assert.Equal("EMP-A1", result.EmployeeId);
        Assert.Single(db.OnboardingRecords.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Assign_CrossTenant_TemplateId_IsDenied()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (_, tplB) = Seed(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(db).AssignAsync(CompanyA, new AssignOnboardingDto
            {
                EmployeeId = "EMP-A1", TemplateId = tplB
            }));

        Assert.Empty(db.OnboardingRecords.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Assign_CrossTenant_EmployeeId_IsDenied()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (tplA, _) = Seed(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(db).AssignAsync(CompanyA, new AssignOnboardingDto
            {
                EmployeeId = "EMP-B1", TemplateId = tplA
            }));

        Assert.Empty(db.OnboardingRecords.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Assign_BothIdentifiersCrossTenant_IsDenied()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (_, tplB) = Seed(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(db).AssignAsync(CompanyA, new AssignOnboardingDto
            {
                EmployeeId = "EMP-B1", TemplateId = tplB
            }));

        Assert.Empty(db.OnboardingRecords.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Assign_UnknownEmployee_IsRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (tplA, _) = Seed(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Build(db).AssignAsync(CompanyA, new AssignOnboardingDto
            {
                EmployeeId = "EMP-DOES-NOT-EXIST", TemplateId = tplA
            }));
    }

    /// <summary>
    /// SuperAdmin (companyId == null) is unrestricted, but must still not be able to
    /// stitch a company-B template onto a company-A employee.
    /// </summary>
    [Fact]
    public async Task Assign_SuperAdmin_MismatchedTenants_IsDenied()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (_, tplB) = Seed(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(db).AssignAsync(null, new AssignOnboardingDto
            {
                EmployeeId = "EMP-A1", TemplateId = tplB
            }));
    }

    [Fact]
    public async Task Assign_SuperAdmin_ConsistentTenants_Succeeds()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (_, tplB) = Seed(db);

        var result = await Build(db).AssignAsync(null, new AssignOnboardingDto
        {
            EmployeeId = "EMP-B1", TemplateId = tplB
        });

        Assert.Equal("EMP-B1", result.EmployeeId);
    }

    /// <summary>
    /// The fail-closed sentinel emitted by BaseController when the companyId claim is
    /// missing/malformed must not grant access to any tenant's data.
    /// </summary>
    [Fact]
    public async Task Assign_MalformedCompanyClaimSentinel_IsDenied()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (tplA, _) = Seed(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(db).AssignAsync(-1, new AssignOnboardingDto
            {
                EmployeeId = "EMP-A1", TemplateId = tplA
            }));
    }

    [Fact]
    public async Task Assign_CrossTenant_AssignedTo_IsDenied()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var (tplA, _) = Seed(db);
        var bobId = db.Employees.IgnoreQueryFilters().Single(e => e.EmployeeCode == "EMP-B1").Id;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(db).AssignAsync(CompanyA, new AssignOnboardingDto
            {
                EmployeeId = "EMP-A1", TemplateId = tplA, AssignedTo = bobId
            }));
    }
}

public class WebhookGlobalSubscriptionAuthorizationTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IWebhookService _svc;

    public WebhookGlobalSubscriptionAuthorizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new WebhookService(
            _db,
            new Mock<IHttpClientFactory>().Object,
            new Mock<ILogger<WebhookService>>().Object,
            Channel.CreateUnbounded<WebhookJob>().Writer,
            new ConfigurationBuilder().Build());
    }

    public void Dispose() => _db.Dispose();

    private int SeedSubscription(int? companyId)
    {
        var sub = new WebhookSubscription
        {
            CompanyId = companyId,
            EventType = "employee.created",
            TargetUrl = "https://hooks.example.com/x",
            Secret    = "s3cret",
            IsActive  = true
        };
        _db.WebhookSubscriptions.Add(sub);
        _db.SaveChanges();
        return sub.Id;
    }

    private bool StillActive(int id) =>
        _db.WebhookSubscriptions.IgnoreQueryFilters().Single(x => x.Id == id).IsActive;

    [Fact]
    public async Task CompanyAdmin_Cannot_Delete_GlobalSubscription()
    {
        var id = SeedSubscription(null);

        Assert.False(await _svc.DeleteAsync(id, companyId: 1));
        Assert.True(StillActive(id));
    }

    [Fact]
    public async Task SuperAdmin_Can_Delete_GlobalSubscription()
    {
        var id = SeedSubscription(null);

        Assert.True(await _svc.DeleteAsync(id, companyId: null));
        Assert.False(StillActive(id));
    }

    [Fact]
    public async Task CompanyAdmin_Can_Delete_OwnSubscription()
    {
        var id = SeedSubscription(1);

        Assert.True(await _svc.DeleteAsync(id, companyId: 1));
        Assert.False(StillActive(id));
    }

    [Fact]
    public async Task CompanyAdmin_Cannot_Delete_OtherCompanySubscription()
    {
        var id = SeedSubscription(2);

        Assert.False(await _svc.DeleteAsync(id, companyId: 1));
        Assert.True(StillActive(id));
    }

    [Fact]
    public async Task MalformedCompanyClaimSentinel_Cannot_Delete_Anything()
    {
        var global = SeedSubscription(null);
        var owned  = SeedSubscription(1);

        Assert.False(await _svc.DeleteAsync(global, companyId: -1));
        Assert.False(await _svc.DeleteAsync(owned, companyId: -1));
        Assert.True(StillActive(global));
        Assert.True(StillActive(owned));
    }
}

public class PayrollLockGuardSuperAdminScopeTests
{
    [Fact]
    public async Task GetLocksAsync_SuperAdmin_NullCompany_ReturnsAllCompanies()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);
        await guard.LockAsync(companyId: 2, month: 7, year: 2026, lockedByUserId: 99);

        var locks = await guard.GetLocksAsync(companyId: null);

        Assert.Equal(2, locks.Count);
        Assert.Contains(locks, l => l.CompanyId == 1);
        Assert.Contains(locks, l => l.CompanyId == 2);
    }

    [Fact]
    public async Task GetLocksAsync_CompanyAdmin_SeesOnlyOwnCompany()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);
        await guard.LockAsync(companyId: 2, month: 7, year: 2026, lockedByUserId: 99);

        var locks = await guard.GetLocksAsync(companyId: 1);

        Assert.Single(locks);
        Assert.Equal(1, locks[0].CompanyId);
    }

    [Fact]
    public async Task GetLocksAsync_SuperAdmin_YearFilter_StillApplies()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2025, lockedByUserId: 99);
        await guard.LockAsync(companyId: 2, month: 7, year: 2026, lockedByUserId: 99);

        var locks = await guard.GetLocksAsync(companyId: null, year: 2026);

        Assert.Single(locks);
        Assert.Equal(2026, locks[0].Year);
    }

    /// <summary>Lock enforcement itself is company-scoped and unchanged by the fix.</summary>
    [Fact]
    public async Task IsLockedAsync_Remains_CompanyScoped()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);

        Assert.True(await guard.IsLockedAsync(1, 7, 2026));
        Assert.False(await guard.IsLockedAsync(2, 7, 2026));
    }
}
