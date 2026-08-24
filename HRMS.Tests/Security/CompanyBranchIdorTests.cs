// Security regression tests: CompanyBranch cross-tenant IDOR (SEC-BRANCH-01).
// P2 removed the ownership checks from GetBranchAsync, UpdateBranchAsync, DeleteBranchAsync.
// These tests verify the P1 protections are correctly restored.
using HRMS.Application.DTOs.Company;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Company;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HRMS.Tests.Security;

public class CompanyBranchIdorTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CompanyBranchService _svc;

    public CompanyBranchIdorTests()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(opts);

        var auditMock = new Mock<IAuditService>();
        _svc = new CompanyBranchService(
            _db,
            auditMock.Object,
            NullLogger<CompanyBranchService>.Instance);
    }

    private async Task<int> SeedBranchAsync(int companyId = 1)
    {
        var branch = new CompanyBranch
        {
            CompanyId = companyId, BranchName = $"Branch-{companyId}",
            City = "Mumbai", Country = "India", CreatedAt = DateTime.UtcNow
        };
        _db.CompanyBranches.Add(branch);
        await _db.SaveChangesAsync();
        return branch.Id;
    }

    // ── GetBranchAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBranchAsync_SameCompany_ReturnsDto()
    {
        var id = await SeedBranchAsync(companyId: 1);
        var result = await _svc.GetBranchAsync(id, callerCompanyId: 1);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetBranchAsync_DifferentCompany_ReturnsNull()
    {
        var id = await SeedBranchAsync(companyId: 1);
        // Caller is company 2 — should not see company 1's branch.
        var result = await _svc.GetBranchAsync(id, callerCompanyId: 2);
        Assert.Null(result);
    }

    // ── UpdateBranchAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBranchAsync_SameCompany_Succeeds()
    {
        var id = await SeedBranchAsync(companyId: 1);
        var dto = new CreateCompanyBranchDto { CompanyId = 1, BranchName = "Updated", City = "Delhi", Country = "India" };

        var result = await _svc.UpdateBranchAsync(id, callerCompanyId: 1, dto);

        Assert.True(result);
        var updated = await _db.CompanyBranches.FindAsync(id);
        Assert.Equal("Updated", updated!.BranchName);
    }

    [Fact]
    public async Task UpdateBranchAsync_DifferentCompany_IsBlocked()
    {
        var id = await SeedBranchAsync(companyId: 1);
        var dto = new CreateCompanyBranchDto { CompanyId = 2, BranchName = "Hacked", City = "Delhi", Country = "India" };

        var result = await _svc.UpdateBranchAsync(id, callerCompanyId: 2, dto);

        Assert.False(result, "Cross-tenant update should be denied.");
        // Verify branch was not modified.
        var unchanged = await _db.CompanyBranches.FindAsync(id);
        Assert.Equal("Branch-1", unchanged!.BranchName);
    }

    // ── DeleteBranchAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBranchAsync_SameCompany_Succeeds()
    {
        var id = await SeedBranchAsync(companyId: 1);

        var result = await _svc.DeleteBranchAsync(id, callerCompanyId: 1);

        Assert.True(result);
        Assert.Null(await _db.CompanyBranches.FindAsync(id));
    }

    [Fact]
    public async Task DeleteBranchAsync_DifferentCompany_IsBlocked()
    {
        var id = await SeedBranchAsync(companyId: 1);

        var result = await _svc.DeleteBranchAsync(id, callerCompanyId: 2);

        Assert.False(result, "Cross-tenant deletion should be denied.");
        // Verify branch still exists.
        Assert.NotNull(await _db.CompanyBranches.FindAsync(id));
    }

    // ── GetBranchesAsync — always company-scoped ──────────────────────────

    [Fact]
    public async Task GetBranchesAsync_OnlyReturnsCallerCompanyBranches()
    {
        await SeedBranchAsync(companyId: 1);
        await SeedBranchAsync(companyId: 1);
        await SeedBranchAsync(companyId: 2); // different company

        var results = await _svc.GetBranchesAsync(companyId: 1);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(1, r.CompanyId));
    }

    public void Dispose() => _db.Dispose();
}
