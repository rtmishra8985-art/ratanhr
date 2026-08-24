using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using HRMS.Application.DTOs.Assets;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Repositories;
using HRMS.Infrastructure.Services;

namespace HRMS.Tests
{
    /// <summary>
    /// Unit tests for <see cref="AssetService"/>.
    /// Each test spins up an isolated in-memory database to avoid state leakage.
    /// </summary>
    public class AssetServiceTests
    {
        private static ApplicationDbContext CreateDb(string dbName)
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(opts);
        }

        private static AssetService CreateService(ApplicationDbContext db)
        {
            var logger = new Mock<ILogger<AssetService>>().Object;
            var repo   = new AssetRepository(db);
            return new AssetService(db, repo, logger);
        }

        // ── CreateAsset ───────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsset_ValidDto_ReturnsAssetWithAvailableStatus()
        {
            // Arrange
            using var db = CreateDb(nameof(CreateAsset_ValidDto_ReturnsAssetWithAvailableStatus));
            var svc = CreateService(db);
            var dto = new CreateAssetDto { Name = "MacBook Pro", AssetCode = "AST-001", PurchasePrice = 2500 };

            // Act
            var result = await svc.CreateAssetAsync(dto, companyId: 1, createdByUserId: "U001");

            // Assert
            result.Should().NotBeNull();
            result.AssetCode.Should().Be("AST-001");
            result.Status.Should().Be("Available");
            result.CurrentValue.Should().Be(2500);
        }

        [Fact]
        public async Task CreateAsset_PersistsHistoryEntry()
        {
            using var db = CreateDb(nameof(CreateAsset_PersistsHistoryEntry));
            var svc = CreateService(db);
            var dto = new CreateAssetDto { Name = "Chair", AssetCode = "AST-002" };

            var asset = await svc.CreateAssetAsync(dto, 1, "U001");
            var history = await svc.GetAssetHistoryAsync(asset.Id, 1);

            history.Should().HaveCount(1);
            history.First().Action.Should().Be("Created");
        }

        // ── AssignAsset ───────────────────────────────────────────────────

        [Fact]
        public async Task AssignAsset_AvailableAsset_SetsStatusToAssigned()
        {
            using var db = CreateDb(nameof(AssignAsset_AvailableAsset_SetsStatusToAssigned));
            var svc = CreateService(db);
            var asset = await svc.CreateAssetAsync(new CreateAssetDto { Name = "Laptop", AssetCode = "AST-010" }, 1, "U001");

            var result = await svc.AssignAssetAsync(asset.Id, new AssignAssetDto { EmployeeId = "EMP001" }, 1, "U001");

            result.Should().NotBeNull();
            result!.Status.Should().Be("Assigned");
            result.AssignedToEmployeeId.Should().Be("EMP001");
        }

        [Fact]
        public async Task AssignAsset_AlreadyAssignedAsset_ThrowsInvalidOperation()
        {
            using var db = CreateDb(nameof(AssignAsset_AlreadyAssignedAsset_ThrowsInvalidOperation));
            var svc = CreateService(db);
            var asset = await svc.CreateAssetAsync(new CreateAssetDto { Name = "Phone", AssetCode = "AST-020" }, 1, "U001");
            await svc.AssignAssetAsync(asset.Id, new AssignAssetDto { EmployeeId = "EMP001" }, 1, "U001");

            Func<Task> act = () => svc.AssignAssetAsync(asset.Id, new AssignAssetDto { EmployeeId = "EMP002" }, 1, "U001");

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // ── ReturnAsset ───────────────────────────────────────────────────

        [Fact]
        public async Task ReturnAsset_AssignedAsset_SetsStatusToAvailable()
        {
            using var db = CreateDb(nameof(ReturnAsset_AssignedAsset_SetsStatusToAvailable));
            var svc = CreateService(db);
            var asset = await svc.CreateAssetAsync(new CreateAssetDto { Name = "Tablet", AssetCode = "AST-030" }, 1, "U001");
            await svc.AssignAssetAsync(asset.Id, new AssignAssetDto { EmployeeId = "EMP001" }, 1, "U001");

            var result = await svc.ReturnAssetAsync(asset.Id, new ReturnAssetDto { Condition = "Good" }, 1, "U001");

            result!.Status.Should().Be("Available");
            result.AssignedToEmployeeId.Should().BeNull();
        }

        [Fact]
        public async Task ReturnAsset_DamagedCondition_SetsStatusToDamaged()
        {
            using var db = CreateDb(nameof(ReturnAsset_DamagedCondition_SetsStatusToDamaged));
            var svc = CreateService(db);
            var asset = await svc.CreateAssetAsync(new CreateAssetDto { Name = "Monitor", AssetCode = "AST-031" }, 1, "U001");
            await svc.AssignAssetAsync(asset.Id, new AssignAssetDto { EmployeeId = "EMP001" }, 1, "U001");

            var result = await svc.ReturnAssetAsync(asset.Id, new ReturnAssetDto { Condition = "Damaged" }, 1, "U001");

            result!.Status.Should().Be("Damaged");
        }

        // ── DeleteAsset ───────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsset_ExistingAsset_RetiresIt()
        {
            using var db = CreateDb(nameof(DeleteAsset_ExistingAsset_RetiresIt));
            var svc = CreateService(db);
            var asset = await svc.CreateAssetAsync(new CreateAssetDto { Name = "Printer", AssetCode = "AST-040" }, 1, "U001");

            var ok = await svc.DeleteAssetAsync(asset.Id, 1);
            var updated = await svc.GetAssetByIdAsync(asset.Id, 1);

            ok.Should().BeTrue();
            updated!.Status.Should().Be("Retired");
        }

        [Fact]
        public async Task DeleteAsset_WrongCompany_ReturnsFalse()
        {
            using var db = CreateDb(nameof(DeleteAsset_WrongCompany_ReturnsFalse));
            var svc = CreateService(db);
            var asset = await svc.CreateAssetAsync(new CreateAssetDto { Name = "Router", AssetCode = "AST-050" }, companyId: 1, "U001");

            var ok = await svc.DeleteAssetAsync(asset.Id, companyId: 2);

            ok.Should().BeFalse();
        }

        // ── GetAssetSummary ───────────────────────────────────────────────

        [Fact]
        public async Task GetAssetSummary_CountsCorrectly()
        {
            using var db = CreateDb(nameof(GetAssetSummary_CountsCorrectly));
            var svc = CreateService(db);

            // Create 3 assets, assign 1
            var a1 = await svc.CreateAssetAsync(new CreateAssetDto { Name = "A1", AssetCode = "C001" }, 1, "U");
            var a2 = await svc.CreateAssetAsync(new CreateAssetDto { Name = "A2", AssetCode = "C002" }, 1, "U");
            var a3 = await svc.CreateAssetAsync(new CreateAssetDto { Name = "A3", AssetCode = "C003" }, 1, "U");
            await svc.AssignAssetAsync(a1.Id, new AssignAssetDto { EmployeeId = "EMP001" }, 1, "U");

            var summary = await svc.GetAssetSummaryAsync(1);

            summary.Total.Should().Be(3);
            summary.Assigned.Should().Be(1);
            summary.Available.Should().Be(2);
        }

        // ── Tenant isolation ──────────────────────────────────────────────

        [Fact]
        public async Task GetAssets_DoesNotReturnAssetsFromDifferentTenant()
        {
            using var db = CreateDb(nameof(GetAssets_DoesNotReturnAssetsFromDifferentTenant));
            var svc = CreateService(db);
            await svc.CreateAssetAsync(new CreateAssetDto { Name = "T1 Asset", AssetCode = "T1-001" }, companyId: 1, "U");
            await svc.CreateAssetAsync(new CreateAssetDto { Name = "T2 Asset", AssetCode = "T2-001" }, companyId: 2, "U");

            var result = await svc.GetAssetsAsync(new AssetQueryDto(), companyId: 1);

            result.Items.Should().HaveCount(1);
            result.Items.Should().AllSatisfy(a => a.AssetCode.Should().StartWith("T1-"));
        }
    }
}
