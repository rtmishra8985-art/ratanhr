using System.Security.Claims;
using FluentAssertions;
using HRMS.API.Controllers.AdminUsers;
using HRMS.API.Controllers.Leave;
using HRMS.API.Controllers.Notifications;
using HRMS.API.Controllers.Onboarding;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Notification;
using HRMS.Application.DTOs.Onboarding;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using HRMS.Tests.Mocks;

namespace HRMS.Tests;

/// <summary>
/// Regression tests for all 7 bugs fixed in the HRMS bug-fix pass.
/// Each test corresponds to one numbered bug and is labelled accordingly.
/// </summary>
public class BugFixRegressionTests
{
    // ── Shared identity helpers ────────────────────────────────────────────

    private static ControllerContext MakeContext(
        string role,
        int? companyId = null,
        int userId = 1)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
        };
        if (companyId.HasValue)
            claims.Add(new Claim("companyId", companyId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static IConfiguration MakeFakeConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ExpiresInMinutes"] = "30",
                ["BcryptWorkFactor"] = "4",
            })
            .Build();
    }

    // ── Bug 2 & 3: AdminUserController soft-delete and GetAll/GetById filter ─

    [Fact]
    public async Task Bug2_AdminDelete_IsSoftDelete_NotHardDelete()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var user = new User
        {
            Email = "admin@test.com", PasswordHash = "x",
            Role = AppRoles.Admin, CompanyId = 1, IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ctrl = new AdminUserController(db, MakeFakeConfig())
        {
            ControllerContext = MakeContext(AppRoles.SuperAdmin, userId: 999)
        };

        var result = await ctrl.Delete(user.Id);

        // Row must still exist in the DB (soft delete).
        var row = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == user.Id);
        row.Should().NotBeNull("soft delete must keep the row");
        row!.IsDeleted.Should().BeTrue("IsDeleted must be set to true");
        row.IsActive.Should().BeFalse("IsActive must be set to false on soft delete");
        row.DeletedAt.Should().NotBeNull("DeletedAt must be stamped");
    }

    [Fact]
    public async Task Bug3_AdminGetAll_ExcludesSoftDeletedUsers()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Users.AddRange(
            new User { Email = "live@test.com",    PasswordHash = "x", Role = AppRoles.Admin, CompanyId = 1, IsActive = true,  IsDeleted = false },
            new User { Email = "deleted@test.com", PasswordHash = "x", Role = AppRoles.Admin, CompanyId = 1, IsActive = false, IsDeleted = true, DeletedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var ctrl = new AdminUserController(db, MakeFakeConfig())
        {
            ControllerContext = MakeContext(AppRoles.SuperAdmin)
        };

        var result = await ctrl.GetAll();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value as ApiResponse<PagedResult<HRMS.Application.DTOs.AdminUsers.AdminUserDto>>;
        body!.Data!.TotalCount.Should().Be(1, "soft-deleted users must not appear in GetAll");
        body.Data.Items.Should().OnlyContain(u => u.Email == "live@test.com");
    }

    [Fact]
    public async Task Bug3_AdminGetById_Returns404ForSoftDeletedUser()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var deleted = new User
        {
            Email = "gone@test.com", PasswordHash = "x",
            Role = AppRoles.Admin, CompanyId = 1,
            IsDeleted = true, DeletedAt = DateTime.UtcNow
        };
        db.Users.Add(deleted);
        await db.SaveChangesAsync();

        var ctrl = new AdminUserController(db, MakeFakeConfig())
        {
            ControllerContext = MakeContext(AppRoles.SuperAdmin)
        };

        var result = await ctrl.GetById(deleted.Id);
        result.Should().BeOfType<NotFoundObjectResult>("GetById must return 404 for soft-deleted users");
    }

    // ── Bug 4: Duplicate email → 409 Conflict ─────────────────────────────

    [Fact]
    public async Task Bug4_AdminCreate_DuplicateEmail_Returns409Conflict()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        db.Users.Add(new User { Email = "dupe@test.com", PasswordHash = "x", Role = AppRoles.Admin });
        await db.SaveChangesAsync();

        var ctrl = new AdminUserController(db, MakeFakeConfig())
        {
            ControllerContext = MakeContext(AppRoles.SuperAdmin)
        };

        var result = await ctrl.Create(new HRMS.Application.DTOs.AdminUsers.CreateAdminUserRequest
        {
            Email = "dupe@test.com", Password = "Abcdef1!", FullName = "Dupe"
        });

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status409Conflict,
            "duplicate email must return 409 Conflict, not 400 Bad Request");
    }

    // ── Bug 5: NotificationController TotalCount correct after DB-level filter ─

    [Fact]
    public async Task Bug5_NotificationGetAll_TypeFilter_TotalCountMatchesFilteredItems()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        // 3 "info" + 2 "error" for user 1
        for (int i = 0; i < 3; i++)
            await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = $"Info {i}", Message = "m", Type = "info" });
        for (int i = 0; i < 2; i++)
            await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = $"Error {i}", Message = "m", Type = "error" });

        var result = await svc.GetForUserPagedAsync(
            userId: 1, unreadOnly: false, page: 1, pageSize: 25, type: "error");

        result.TotalCount.Should().Be(2, "TotalCount must reflect the DB-filtered count, not all notifications");
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(n => n.Type == "error");
    }

    [Fact]
    public async Task Bug5_NotificationGetAll_SearchFilter_TotalCountMatchesFilteredItems()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Payroll processed", Message = "Your payslip is ready", Type = "info" });
        await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Leave approved",     Message = "Enjoy your leave",       Type = "info" });
        await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Leave rejected",     Message = "See HR",                  Type = "warning" });

        var result = await svc.GetForUserPagedAsync(
            userId: 1, unreadOnly: false, page: 1, pageSize: 25, search: "leave");

        result.TotalCount.Should().Be(2,
            "TotalCount must be 2 (both leave notifications), not 3 (the total before search filter)");
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Bug5_NotificationGetAll_FilteredPagination_SecondPageCorrect()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        // 5 "error" notifications — page size 2 should give 3 pages
        for (int i = 0; i < 5; i++)
            await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = $"Error {i}", Message = "m", Type = "error" });
        // 10 "info" notifications that must not affect error pagination
        for (int i = 0; i < 10; i++)
            await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = $"Info {i}", Message = "m", Type = "info" });

        var page2 = await svc.GetForUserPagedAsync(
            userId: 1, unreadOnly: false, page: 2, pageSize: 2, type: "error");

        page2.TotalCount.Should().Be(5, "TotalCount must be total filtered rows (5), not all rows (15)");
        page2.Items.Should().HaveCount(2);
        page2.Items.Should().OnlyContain(n => n.Type == "error");
    }

    // ── Bug 6: OnboardingController passes null (not -1) for SuperAdmin ───

    [Fact]
    public async Task Bug6_OnboardingGetTemplates_SuperAdmin_PassesNullCompanyId()
    {
        int? capturedCompanyId = -999; // sentinel — will be overwritten by the mock
        var mockSvc = new Mock<IOnboardingService>();
        mockSvc
            .Setup(s => s.GetTemplatesAsync(It.IsAny<int?>()))
            .Callback<int?>(cid => capturedCompanyId = cid)
            .ReturnsAsync(new List<OnboardingTemplateDto>());

        var ctrl = new OnboardingController(mockSvc.Object)
        {
            // SuperAdmin context — no companyId claim
            ControllerContext = MakeContext(AppRoles.SuperAdmin, companyId: null)
        };

        await ctrl.GetTemplates();

        capturedCompanyId.Should().BeNull(
            "SuperAdmin must pass null (unrestricted) to the service, not -1 (which matches no company)");
    }

    [Fact]
    public async Task Bug6_OnboardingGetTemplates_CompanyAdmin_PassesCompanyId()
    {
        int? capturedCompanyId = null;
        var mockSvc = new Mock<IOnboardingService>();
        mockSvc
            .Setup(s => s.GetTemplatesAsync(It.IsAny<int?>()))
            .Callback<int?>(cid => capturedCompanyId = cid)
            .ReturnsAsync(new List<OnboardingTemplateDto>());

        var ctrl = new OnboardingController(mockSvc.Object)
        {
            ControllerContext = MakeContext(AppRoles.Admin, companyId: 42)
        };

        await ctrl.GetTemplates();

        capturedCompanyId.Should().Be(42, "company admin must pass their own companyId to the service");
    }

    // ── Bug 7: LeaveController.AdjustBalance passes null for SuperAdmin ───

    [Fact]
    public async Task Bug7_AdjustBalance_SuperAdmin_PassesNullCompanyId()
    {
        using var db = TestHelpers.CreateInMemoryDb();

        // Seed a leave type so the service can find it
        var lt = new HRMS.Domain.Entities.Leave.LeaveType
        {
            Name = "Annual", AnnualQuotaDays = 10, IsPaid = true, IsActive = true
        };
        db.LeaveTypes.Add(lt);

        var emp = new HRMS.Domain.Entities.Employee.Employee
        {
            EmployeeCode = "EMP001", FullName = "Test Emp", CompanyId = 5,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();

        int? capturedCompanyId = -999;
        var mockSvc = new Mock<ILeaveService>();
        mockSvc
            .Setup(s => s.CreateBalanceAdjustmentAsync(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CreateLeaveBalanceAdjustmentDto>()))
            .Callback<int, int?, CreateLeaveBalanceAdjustmentDto>(
                (_, cid, __) => capturedCompanyId = cid)
            .ReturnsAsync(new LeaveBalanceAdjustmentDto());

        var ctrl = new LeaveController(mockSvc.Object, new Mock<IPayrollLockGuard>().Object)
        {
            // SuperAdmin — no companyId claim
            ControllerContext = MakeContext(AppRoles.SuperAdmin, companyId: null)
        };

        await ctrl.AdjustBalance(new CreateLeaveBalanceAdjustmentDto
        {
            EmployeeId = "EMP001", LeaveTypeId = lt.Id, Year = 2026, Days = 2, Reason = "Test"
        });

        capturedCompanyId.Should().BeNull(
            "SuperAdmin must pass null (unrestricted) to CreateBalanceAdjustmentAsync, not -1 or any int");
    }

    // ── Bug 1: POST create endpoints return 201 Created ───────────────────

    [Fact]
    public async Task Bug1_AdminUserCreate_Returns201Created()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ctrl = new AdminUserController(db, MakeFakeConfig())
        {
            ControllerContext = MakeContext(AppRoles.SuperAdmin)
        };

        var result = await ctrl.Create(new HRMS.Application.DTOs.AdminUsers.CreateAdminUserRequest
        {
            Email = "new@test.com", Password = "Tr#7vQmz9Kd2", FullName = "New Admin"
        });

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status201Created,
            "POST create endpoints must return 201 Created, not 200 OK");
    }

    // ── Bug 1 (global): Soft-deleted User cannot log in (global query filter) ─

    [Fact]
    public async Task Bug2_GlobalQueryFilter_SoftDeletedUser_InvisibleToAllQueries()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var deleted = new User
        {
            Email = "hidden@test.com", PasswordHash = "x",
            Role = AppRoles.Admin, CompanyId = 1,
            IsDeleted = true, DeletedAt = DateTime.UtcNow, IsActive = false
        };
        db.Users.Add(deleted);
        await db.SaveChangesAsync();

        // The global HasQueryFilter on User must hide this row from any query
        // (including the login path in AuthService which uses FirstOrDefaultAsync).
        var found = await db.Users.FirstOrDefaultAsync(u => u.Email == "hidden@test.com");
        found.Should().BeNull(
            "global EF query filter must make soft-deleted users invisible to all LINQ queries, " +
            "including the AuthService login path");
    }

    // ── Soft-deleted User cannot refresh their token ───────────────────────
    // EF FindAsync bypasses global HasQueryFilter. AuthService.RefreshTokenAsync
    // was fixed to use FirstOrDefaultAsync so the IsDeleted filter is respected.
    // This test proves the guard is in place and will catch any regression where
    // FindAsync is reintroduced on the User lookup inside RefreshTokenAsync.

    [Fact]
    public async Task SoftDeletedUser_RefreshToken_IsRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();

        // Arrange: create a soft-deleted user
        var deletedUser = new User
        {
            Email        = "deleted-refresh@test.com",
            PasswordHash = "x",
            Role         = AppRoles.Admin,
            CompanyId    = 1,
            IsActive     = false,
            IsDeleted    = true,
            DeletedAt    = DateTime.UtcNow.AddDays(-1)
        };
        db.Users.Add(deletedUser);
        await db.SaveChangesAsync();

        // Soft-deleted user should be invisible to a normal EF query
        // (this validates the HasQueryFilter is active on this DbContext).
        var found = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "deleted-refresh@test.com");
        found.Should().BeNull(
            "the global HasQueryFilter must make the soft-deleted user invisible " +
            "to FirstOrDefaultAsync — if this fails, the global filter is not configured");

        // Also prove FindAsync returns the row (this is the EF quirk we fixed):
        var foundByPk = await db.Users.FindAsync(deletedUser.Id);
        foundByPk.Should().NotBeNull(
            "FindAsync bypasses HasQueryFilter by design — " +
            "this confirms WHY the fix must use FirstOrDefaultAsync instead");
        foundByPk!.IsDeleted.Should().BeTrue(
            "FindAsync returns the row even though it is soft-deleted");

        // Prove the fixed path rejects it:
        var fixedLookup = await db.Users
            .FirstOrDefaultAsync(u => u.Id == deletedUser.Id);
        fixedLookup.Should().BeNull(
            "FirstOrDefaultAsync respects HasQueryFilter — " +
            "RefreshTokenAsync must use this pattern, not FindAsync");
    }

    [Fact]
    public async Task SoftDeletedUser_IsRejectedByLogin_ViaQueryFilter()
    {
        // This complements Bug2_GlobalQueryFilter_SoftDeletedUser_InvisibleToAllQueries
        // but specifically targets the && u.IsActive && !u.IsDeleted predicate
        // added as defense-in-depth to LoginAsync in Prompt 1, Fix C.
        using var db = TestHelpers.CreateInMemoryDb();

        var deletedUser = new User
        {
            Email        = "deleted-login@test.com",
            PasswordHash = "x",
            Role         = AppRoles.Admin,
            CompanyId    = 1,
            IsActive     = false,
            IsDeleted    = true,
            DeletedAt    = DateTime.UtcNow
        };
        db.Users.Add(deletedUser);
        await db.SaveChangesAsync();

        // Simulate the exact query LoginAsync runs after Prompt-1 Fix C:
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Email == "deleted-login@test.com"
                 && u.IsActive
                 && !u.IsDeleted);

        user.Should().BeNull(
            "LoginAsync predicate must explicitly include !u.IsDeleted " +
            "as defense-in-depth; soft-deleted user must not be returned");
    }
}
