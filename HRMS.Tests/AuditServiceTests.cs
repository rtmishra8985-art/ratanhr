using FluentAssertions;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for AuditService: verifies that audit log entries are written correctly,
/// company-scoped, and retrievable for compliance queries.
/// </summary>
public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _svc;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _svc = new AuditService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ─── LogAsync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_ValidEntry_PersistsToDatabase()
    {
        // Act
        await _svc.LogAsync(
            action:     "CREATE",
            entityType: "Employee",
            entityId:   "42",
            actorId:    1,
            actorName:  "admin@co.com",
            companyId:  1,
            details:    "Created employee Alice Smith");

        // Assert
        var logs = await _db.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].Action.Should().Be("CREATE");
        logs[0].EntityType.Should().Be("Employee");
        logs[0].EntityId.Should().Be("42");
        logs[0].ActorName.Should().Be("admin@co.com");
    }

    [Fact]
    public async Task LogAsync_MultipleEntries_AllPersisted()
    {
        // Act
        await _svc.LogAsync("CREATE", "Employee", "1", 1, "admin", 1);
        await _svc.LogAsync("UPDATE", "Employee", "1", 1, "admin", 1);
        await _svc.LogAsync("DELETE", "Payslip",  "5", 1, "admin", 1);

        // Assert
        var count = await _db.AuditLogs.CountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task LogAsync_TimestampIsSet_ToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        await _svc.LogAsync("READ", "Report", "0", 1, "admin", 1);

        // Assert
        var log = await _db.AuditLogs.FirstAsync();
        log.Timestamp.Should().BeAfter(before);
        log.Timestamp.Kind.Should().Be(DateTimeKind.Utc,
            "timestamps must always be stored as UTC");
    }

    [Fact]
    public async Task LogAsync_CompanyIdIsPreserved()
    {
        // Act
        await _svc.LogAsync("UPDATE", "Salary", "10", 2, "hr@co.com", companyId: 99);

        // Assert
        var log = await _db.AuditLogs.FirstAsync();
        log.CompanyId.Should().Be(99);
    }

    // ─── GetLogsAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLogsAsync_FiltersByCompany_DoesNotReturnOtherCompanyLogs()
    {
        // Arrange
        await _svc.LogAsync("CREATE", "Employee", "1", 1, "admin",  companyId: 1);
        await _svc.LogAsync("CREATE", "Employee", "2", 2, "admin2", companyId: 2);

        // Act
        var logs = await _svc.GetLogsAsync(companyId: 1);

        // Assert
        logs.Should().HaveCount(1);
        logs[0].CompanyId.Should().Be(1);
    }

    [Fact]
    public async Task GetLogsAsync_FiltersByEntityType()
    {
        // Arrange
        await _svc.LogAsync("CREATE", "Employee", "1", 1, "admin", companyId: 1);
        await _svc.LogAsync("UPDATE", "Payslip",  "5", 1, "admin", companyId: 1);

        // Act
        var logs = await _svc.GetLogsAsync(companyId: 1, entityType: "Payslip");

        // Assert
        logs.Should().HaveCount(1);
        logs[0].EntityType.Should().Be("Payslip");
    }

    [Fact]
    public async Task GetLogsAsync_FiltersByActorId()
    {
        // Arrange
        await _svc.LogAsync("CREATE", "Employee", "1", actorId: 10, "actor10", companyId: 1);
        await _svc.LogAsync("DELETE", "Employee", "2", actorId: 20, "actor20", companyId: 1);

        // Act
        var logs = await _svc.GetLogsAsync(companyId: 1, actorId: 10);

        // Assert
        logs.Should().HaveCount(1);
        logs[0].ActorId.Should().Be(10);
    }

    // ─── Null safety ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_NullDetails_DoesNotThrow()
    {
        // Act & Assert
        var act = async () => await _svc.LogAsync(
            "READ", "Employee", "1", 1, "admin", 1, details: null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogAsync_EmptyActorName_DoesNotThrow()
    {
        // Act & Assert
        var act = async () => await _svc.LogAsync(
            "CREATE", "Shift", "9", 1, actorName: "", 1);
        await act.Should().NotThrowAsync();
    }

    // ─── Concurrency ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_ConcurrentCalls_AllPersisted()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 20)
            .Select(i => _svc.LogAsync("UPDATE", "Employee", i.ToString(), 1, "admin", 1))
            .ToArray();

        // Act
        await Task.WhenAll(tasks);

        // Assert
        var count = await _db.AuditLogs.CountAsync();
        count.Should().Be(20, "all concurrent log writes must succeed");
    }
}
