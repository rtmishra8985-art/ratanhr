using FluentAssertions;
using HRMS.Application.DTOs;
using HRMS.Application.DTOs.AdminUsers;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Authentication;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for AdminUserService: user creation, role assignment, company isolation,
/// password reset, and permission checks.
/// </summary>
public class AdminUserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminUserService    _svc;
    private const int CompanyId = 1;

    // Captured after SeedData so tests can reference seeded IDs.
    private int _user1Id, _user2Id, _user3Id;

    public AdminUserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options, config: null, tenant: null);
        // BUG FIX (test update): AdminUserService now takes an IConfiguration so it can
        // hash passwords via BcryptPasswordHasher.Hash (reads Security:BcryptWorkFactor)
        // instead of BCrypt.Net.BCrypt.HashPassword's uncomfigurable default work factor.
        // An empty configuration is fine here since BcryptPasswordHasher falls back to its
        // own DefaultWorkFactor (12) when the key is absent.
        var config = new ConfigurationBuilder().Build();
        _svc = new AdminUserService(
            _db,
            new Mock<IAuditService>().Object,
            new Mock<IEmailService>().Object,
            config);
        SeedData();
    }

    public void Dispose() => _db.Dispose();

    // ─── GetAll ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminsByCompanyAsync_ReturnsScopedToCompany()
    {
        var users = await _svc.GetAdminsByCompanyAsync(CompanyId);

        users.Should().HaveCount(2);
        users.All(u => u.CompanyId == CompanyId).Should().BeTrue();
    }

    [Fact]
    public async Task GetAdminsByCompanyAsync_DoesNotReturnOtherCompanyAdmins()
    {
        var users = await _svc.GetAdminsByCompanyAsync(CompanyId);

        users.Any(u => u.CompanyId == 2).Should().BeFalse();
    }

    // ─── GetById ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminByIdAsync_SameCompany_ReturnsUser()
    {
        var user = await _svc.GetAdminByIdAsync(_user1Id, CompanyId);

        user.Should().NotBeNull();
        user!.Email.Should().Be("admin1@co.com");
    }

    [Fact]
    public async Task GetAdminByIdAsync_CrossCompany_ReturnsNull()
    {
        // user-3 belongs to company 2
        var user = await _svc.GetAdminByIdAsync(_user3Id, CompanyId);

        user.Should().BeNull("cross-company access must be denied");
    }

    // ─── Create ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAdminAsync_ValidDto_CreatesUser()
    {
        var dto = new CreateAdminUserDto
        {
            Email    = "newadmin@co.com",
            FullName = "New Admin",
            Role     = "admin",
            Password = "Tr#7vQmz9Kd2"
        };

        var newId = await _svc.CreateAdminAsync(CompanyId, dto);

        newId.Should().BeGreaterThan(0);
        var saved = await _db.Users.FindAsync(newId);
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("newadmin@co.com");
        saved.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public async Task CreateAdminAsync_DuplicateEmail_ThrowsException()
    {
        var dto = new CreateAdminUserDto
        {
            Email    = "admin1@co.com",   // already exists
            FullName = "Dup",
            Role     = "admin",
            Password = "Admin@1234"
        };

        var act = async () => await _svc.CreateAdminAsync(CompanyId, dto);
        await act.Should().ThrowAsync<Exception>("duplicate email must be rejected");
    }

    [Fact]
    public async Task CreateAdminAsync_WeakPassword_ThrowsOrReturnsFail()
    {
        var dto = new CreateAdminUserDto
        {
            Email    = "weak@co.com",
            FullName = "Weak",
            Role     = "admin",
            Password = "abc"             // too short
        };

        var act = async () => await _svc.CreateAdminAsync(CompanyId, dto);
        await act.Should().ThrowAsync<Exception>("weak password must be rejected");
    }

    // ─── Role assignment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRoleAsync_ValidRole_UpdatesRole()
    {
        var success = await _svc.AssignRoleAsync(_user1Id, CompanyId, "admin");

        success.Should().BeTrue();
        var user = await _db.Users.FindAsync(_user1Id);
        user!.Role.Should().Be("admin");
    }

    [Fact]
    public async Task AssignRoleAsync_SuperAdminRole_ForbiddenForNonSuperAdmin()
    {
        var act = async () => await _svc.AssignRoleAsync(_user1Id, CompanyId, "superadmin");

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "only SuperAdmin can grant the SuperAdmin role");
    }

    // ─── Password reset ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_SameCompany_Succeeds()
    {
        var success = await _svc.ResetPasswordAsync(_user1Id, CompanyId, "NewPass@1234");

        success.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_CrossCompany_ReturnsFalse()
    {
        // _user3Id belongs to company 2 — company 1 must not be able to reset it
        var success = await _svc.ResetPasswordAsync(_user3Id, CompanyId, "NewPass@1234");

        success.Should().BeFalse();
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────────

    private void SeedData()
    {
        var u1 = new User { CompanyId = 1, Email = "admin1@co.com", FullName = "Admin One",   Role = "admin", IsActive = true };
        var u2 = new User { CompanyId = 1, Email = "admin2@co.com", FullName = "Admin Two",   Role = "admin", IsActive = true };
        var u3 = new User { CompanyId = 2, Email = "admin3@co.com", FullName = "Admin Three", Role = "admin", IsActive = true };
        _db.Users.AddRange(u1, u2, u3);
        _db.SaveChanges();
        _user1Id = u1.Id;
        _user2Id = u2.Id;
        _user3Id = u3.Id;
    }
}
