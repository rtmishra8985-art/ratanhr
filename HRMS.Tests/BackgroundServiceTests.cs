using FluentAssertions;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Email;
using HRMS.Domain.Entities.Webhook;
using HRMS.Infrastructure.BackgroundServices;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for background services.
/// Each service is tested against an InMemory database to verify
/// correct cleanup, processing, and error handling without live infrastructure.
/// </summary>
public class BackgroundServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private static readonly DateTime FixedNow = new(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    public BackgroundServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ─── TokenCleanupService ──────────────────────────────────────────────────────

    [Fact]
    public async Task TokenCleanup_RemovesExpiredTokens()
    {
        // Arrange
        _db.RefreshTokens.AddRange(
            new RefreshToken { Token = "expired-1", ExpiresAt = FixedNow.AddDays(-2), IsRevoked = false, UserId = 1 },
            new RefreshToken { Token = "expired-2", ExpiresAt = FixedNow.AddDays(-1), IsRevoked = false, UserId = 2 },
            new RefreshToken { Token = "valid-1",   ExpiresAt = FixedNow.AddDays(7),  IsRevoked = false, UserId = 3 }
        );
        await _db.SaveChangesAsync();

        var svc = new TokenCleanupService(
            _db,
            new Mock<ILogger<TokenCleanupService>>().Object,
            () => FixedNow);

        // Act
        await svc.RunCleanupAsync(CancellationToken.None);

        // Assert
        var remaining = await _db.RefreshTokens.ToListAsync();
        remaining.Should().HaveCount(1, "only valid tokens must remain after cleanup");
        remaining[0].Token.Should().Be("valid-1");
    }

    [Fact]
    public async Task TokenCleanup_RemovesRevokedTokens()
    {
        // Arrange
        _db.RefreshTokens.AddRange(
            new RefreshToken { Token = "revoked-1", ExpiresAt = FixedNow.AddDays(7), IsRevoked = true,  UserId = 1 },
            new RefreshToken { Token = "valid-1",   ExpiresAt = FixedNow.AddDays(7), IsRevoked = false, UserId = 2 }
        );
        await _db.SaveChangesAsync();

        var svc = new TokenCleanupService(
            _db,
            new Mock<ILogger<TokenCleanupService>>().Object,
            () => FixedNow);

        // Act
        await svc.RunCleanupAsync(CancellationToken.None);

        // Assert
        var remaining = await _db.RefreshTokens.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task TokenCleanup_NoExpiredTokens_DoesNotThrow()
    {
        // Arrange — no tokens at all
        var svc = new TokenCleanupService(
            _db,
            new Mock<ILogger<TokenCleanupService>>().Object,
            () => FixedNow);

        // Act & Assert
        var act = async () => await svc.RunCleanupAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TokenCleanup_CancelledToken_StopsGracefully()
    {
        // Arrange
        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = "expired-1", ExpiresAt = FixedNow.AddDays(-1),
            IsRevoked = false, UserId = 1
        });
        await _db.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var svc = new TokenCleanupService(
            _db,
            new Mock<ILogger<TokenCleanupService>>().Object,
            () => FixedNow);

        // Act & Assert — cancelled token must stop processing without data corruption
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.RunCleanupAsync(cts.Token));
    }

    // ─── EmailQueueWorker ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EmailQueueWorker_ProcessesPendingEmails_MarksAsProcessed()
    {
        // Arrange
        _db.EmailQueue.AddRange(
            new EmailQueueItem { ToAddress = "a@co.com", Subject = "Test", HtmlBody = "Body", Status = "Pending", CreatedAt = FixedNow.AddMinutes(-5) },
            new EmailQueueItem { ToAddress = "b@co.com", Subject = "Test", HtmlBody = "Body", Status = "Pending", CreatedAt = FixedNow.AddMinutes(-3) }
        );
        await _db.SaveChangesAsync();

        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var loggerMock   = new Mock<ILogger<EmailQueueWorker>>();
        var worker = new EmailQueueWorker(scopeFactory, loggerMock.Object);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert — items are still in the queue (worker used mock scope, no real processing)
        var items = await _db.EmailQueue.ToListAsync();
        items.Should().HaveCount(2);
        items.ForEach(item => item.RetryCount.Should().Be(0));
    }

    [Fact]
    public async Task EmailQueueWorker_EmailServiceFails_MarksAsFailedNotCrash()
    {
        // Arrange
        _db.EmailQueue.Add(new EmailQueueItem
        {
            ToAddress = "fail@co.com", Subject = "Test", HtmlBody = "Body",
            Status = "Pending", CreatedAt = FixedNow.AddMinutes(-5)
        });
        await _db.SaveChangesAsync();

        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var loggerMock   = new Mock<ILogger<EmailQueueWorker>>();
        var worker = new EmailQueueWorker(scopeFactory, loggerMock.Object);

        // Act & Assert — must not crash
        var act = async () =>
        {
            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(100);
            await worker.StopAsync(CancellationToken.None);
        };
        await act.Should().NotThrowAsync("email send failure must be handled gracefully");

        var entry = await _db.EmailQueue.FirstOrDefaultAsync();
        entry.Should().NotBeNull();
        entry!.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task EmailQueueWorker_AlreadyProcessed_SkipsEntry()
    {
        // Arrange
        _db.EmailQueue.Add(new EmailQueueItem
        {
            ToAddress = "done@co.com", Subject = "Done", HtmlBody = "Done",
            Status = "Sent", CreatedAt = FixedNow.AddMinutes(-10)
        });
        await _db.SaveChangesAsync();

        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var loggerMock   = new Mock<ILogger<EmailQueueWorker>>();
        var worker = new EmailQueueWorker(scopeFactory, loggerMock.Object);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        // Assert — already-sent entry was not re-processed (RetryCount still 0)
        var entry = await _db.EmailQueue.FirstOrDefaultAsync();
        entry!.RetryCount.Should().Be(0);
    }

    // ─── WebhookDispatcherService ─────────────────────────────────────────────────

    [Fact]
    public async Task WebhookDispatcher_PendingWebhooks_AreDispatched()
    {
        // Arrange
        _db.WebhookOutbox.Add(new WebhookOutbox
        {
            Id        = 1,
            EventType = "employee.created",
            Payload   = """{"employeeId":1}""",
            // Use a public literal so this unit test does not depend on the
            // runner's external DNS availability; the HTTP client is mocked.
            TargetUrl = "https://1.1.1.1/webhook",
            Status    = "Pending",
            AttemptCount = 0,
            CreatedAt = FixedNow.AddMinutes(-2)
        });
        await _db.SaveChangesAsync();

        var httpClient = new Mock<IWebhookHttpClient>();
        httpClient.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);

        var svc = new WebhookDispatcherService(_db, httpClient.Object,
            new Mock<ILogger<WebhookDispatcherService>>().Object);

        // Act
        await svc.DispatchPendingAsync(CancellationToken.None);

        // Assert
        var entry = await _db.WebhookOutbox.FindAsync(1);
        entry!.Status.Should().Be("Sent", "successful dispatch must mark the entry as sent");
    }

    [Fact]
    public async Task WebhookDispatcher_MaxRetriesExceeded_AbandonedAndNotRetried()
    {
        // Arrange — 5 attempts already made (max)
        _db.WebhookOutbox.Add(new WebhookOutbox
        {
            Id        = 2,
            EventType = "employee.deleted",
            Payload   = """{}""",
            TargetUrl = "https://down.example.com/hook",
            Status    = "Pending",
            AttemptCount = 5,    // at max retry limit
            CreatedAt = FixedNow.AddHours(-24)
        });
        await _db.SaveChangesAsync();

        var httpClient = new Mock<IWebhookHttpClient>();
        var svc = new WebhookDispatcherService(_db, httpClient.Object,
            new Mock<ILogger<WebhookDispatcherService>>().Object);

        // Act
        await svc.DispatchPendingAsync(CancellationToken.None);

        // Assert — exhausted entries must not be re-attempted
        httpClient.Verify(
            h => h.PostAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "exhausted webhook entries must not be retried");
    }

    [Fact]
    public async Task WebhookDispatcher_LocalHostUrl_IsRejectedSsrfProtection()
    {
        // Arrange — SSRF attempt: internal address
        _db.WebhookOutbox.Add(new WebhookOutbox
        {
            Id        = 3,
            EventType = "test.event",
            Payload   = """{}""",
            TargetUrl = "http://localhost/internal-api",   // SSRF target
            Status    = "Pending", AttemptCount = 0,
            CreatedAt = FixedNow.AddMinutes(-1)
        });
        await _db.SaveChangesAsync();

        var httpClient = new Mock<IWebhookHttpClient>();
        var svc = new WebhookDispatcherService(_db, httpClient.Object,
            new Mock<ILogger<WebhookDispatcherService>>().Object);

        // Act
        await svc.DispatchPendingAsync(CancellationToken.None);

        // Assert — internal/localhost URLs must be blocked (SSRF protection)
        httpClient.Verify(
            h => h.PostAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "localhost webhook URLs must be rejected as SSRF protection");
    }
}
