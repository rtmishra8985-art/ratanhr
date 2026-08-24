using FluentAssertions;
using HRMS.Application.DTOs.Webhook;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Webhook;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for WebhookService: event registration, delivery, retry logic,
/// and SSRF protection.
/// </summary>
public class WebhookServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IWebhookService _svc;
    private const int CompanyId = 1;

    public WebhookServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        var httpClientFactory = new Mock<IHttpClientFactory>().Object;
        var config = new ConfigurationBuilder().Build();
        _svc = new WebhookService(
            _db,
            httpClientFactory,
            new Mock<ILogger<WebhookService>>().Object,
            Channel.CreateUnbounded<WebhookJob>().Writer,
            config);
    }

    public void Dispose() => _db.Dispose();

    // ─── Register ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterWebhookAsync_ValidUrl_PersistsSubscription()
    {
        // Arrange
        var dto = new CreateWebhookDto
        {
            CompanyId  = CompanyId,
            EventType  = "employee.created",
            TargetUrl  = "https://partner.example.com/webhook",
            SecretKey  = "mysecret"
        };

        // Act
        var id = await _svc.RegisterAsync(CompanyId, dto);

        // Assert
        id.Id.Should().BeGreaterThan(0);
        var saved = await _db.WebhookSubscriptions.FindAsync(id.Id);
        saved.Should().NotBeNull();
        saved!.EventType.Should().Be("employee.created");
        saved.TargetUrl.Should().Be("https://partner.example.com/webhook");
    }

    [Fact]
    public async Task RegisterWebhookAsync_HttpUrl_IsRejected()
    {
        // Arrange — HTTP (not HTTPS) webhooks should be rejected
        var dto = new CreateWebhookDto
        {
            CompanyId = CompanyId,
            EventType = "employee.created",
            TargetUrl = "http://insecure.example.com/webhook",
            SecretKey = "mysecret"
        };

        // Act & Assert
        var act = async () => await _svc.RegisterAsync(CompanyId, dto);
        await act.Should().ThrowAsync<Exception>("non-HTTPS webhook URLs must be rejected");
    }

    [Fact]
    public async Task RegisterWebhookAsync_LocalhostUrl_IsRejected()
    {
        // Arrange — SSRF protection: localhost must be blocked
        var dto = new CreateWebhookDto
        {
            CompanyId = CompanyId,
            EventType = "employee.created",
            TargetUrl = "https://localhost/internal",
            SecretKey = "mysecret"
        };

        // Act & Assert
        var act = async () => await _svc.RegisterAsync(CompanyId, dto);
        await act.Should().ThrowAsync<Exception>("localhost webhook URL is an SSRF risk and must be rejected");
    }

    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data/")]      // AWS metadata
    [InlineData("https://10.0.0.1/internal")]                       // RFC1918 private
    [InlineData("https://192.168.1.1/admin")]                       // RFC1918 private
    [InlineData("https://172.16.0.1/secret")]                       // RFC1918 private
    public async Task RegisterWebhookAsync_PrivateIpUrl_IsRejected(string url)
    {
        // Arrange
        var dto = new CreateWebhookDto
        {
            CompanyId = CompanyId,
            EventType = "test.event",
            TargetUrl = url,
            SecretKey = "mysecret"
        };

        // Act & Assert
        var act = async () => await _svc.RegisterAsync(CompanyId, dto);
        await act.Should().ThrowAsync<Exception>($"Private/internal URL {url} must be blocked (SSRF)");
    }

    // ─── GetAll / company isolation ───────────────────────────────────────────────

    [Fact]
    public async Task GetWebhooksAsync_ReturnsOnlyCompanySubscriptions()
    {
        // Arrange
        _db.WebhookSubscriptions.AddRange(
            new WebhookSubscription { Id = 1, CompanyId = 1, EventType = "e.created", TargetUrl = "https://a.com/hook", IsActive = true },
            new WebhookSubscription { Id = 2, CompanyId = 2, EventType = "e.created", TargetUrl = "https://b.com/hook", IsActive = true }
        );
        await _db.SaveChangesAsync();

        // Act
        var subs = await _svc.ListAsync(CompanyId);

        // Assert
        subs.Should().HaveCount(1);
        subs.All(s => s.CompanyId == CompanyId).Should().BeTrue();
    }

    // ─── Delete / deactivate ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteWebhookAsync_SameCompany_Succeeds()
    {
        // Arrange
        _db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            Id = 10, CompanyId = CompanyId,
            EventType = "e.created", TargetUrl = "https://a.com/hook", IsActive = true
        });
        await _db.SaveChangesAsync();

        // Act
        var success = await _svc.DeleteWebhookAsync(10, CompanyId);

        // Assert
        success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteWebhookAsync_CrossCompany_ReturnsFalse()
    {
        // Arrange
        _db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            Id = 20, CompanyId = 2,
            EventType = "e.created", TargetUrl = "https://b.com/hook", IsActive = true
        });
        await _db.SaveChangesAsync();

        // Act — company 1 tries to delete company 2's webhook
        var success = await _svc.DeleteWebhookAsync(20, CompanyId);

        // Assert
        success.Should().BeFalse("cross-company webhook deletion must be rejected");
    }

    // ─── Dispatch / enqueue ───────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchEventAsync_MatchingSubscribers_EnqueuesOutboxEntries()
    {
        _db.WebhookSubscriptions.AddRange(
            new WebhookSubscription
            {
                Id = 1, CompanyId = CompanyId, EventType = "employee.created",
                TargetUrl = "https://partner.example.com/webhook",
                Secret = "company-secret", IsActive = true
            },
            new WebhookSubscription
            {
                Id = 2, CompanyId = 2, EventType = "employee.created",
                TargetUrl = "https://other.example.com/webhook",
                Secret = "other-secret", IsActive = true
            },
            new WebhookSubscription
            {
                Id = 3, CompanyId = CompanyId, EventType = "employee.updated",
                TargetUrl = "https://partner.example.com/webhook",
                Secret = "different-event-secret", IsActive = true
            });
        await _db.SaveChangesAsync();

        await _svc.DispatchEventAsync(CompanyId, "employee.created", """{"employeeId":1}""");

        var entries = await _db.WebhookOutbox.ToListAsync();
        entries.Should().ContainSingle();
        entries[0].CompanyId.Should().Be(CompanyId);
        entries[0].SubscriptionId.Should().Be(1);
        entries[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task DispatchEventAsync_NoMatchingSubscribers_DoesNotEnqueue()
    {
        _db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            CompanyId = CompanyId,
            EventType = "employee.updated",
            TargetUrl = "https://partner.example.com/webhook",
            Secret = "company-secret",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        await _svc.DispatchEventAsync(CompanyId, "employee.created", "{}");

        (await _db.WebhookOutbox.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DispatchEventAsync_InactiveSubscription_IsSkipped()
    {
        _db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            CompanyId = CompanyId,
            EventType = "employee.created",
            TargetUrl = "https://partner.example.com/webhook",
            Secret = "company-secret",
            IsActive = false
        });
        await _db.SaveChangesAsync();

        await _svc.DispatchEventAsync(CompanyId, "employee.created", "{}");

        (await _db.WebhookOutbox.CountAsync()).Should().Be(0);
    }

    // ─── Signature ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchEventAsync_OutboxEntry_HasSignature()
    {
        const string secret = "company-secret";
        const string payload = """{"employeeId":1}""";
        _db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            CompanyId = CompanyId,
            EventType = "employee.created",
            TargetUrl = "https://partner.example.com/webhook",
            Secret = secret,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        await _svc.DispatchEventAsync(CompanyId, "employee.created", payload);

        var entry = await _db.WebhookOutbox.SingleAsync();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        entry.Signature.Should().Be(expected);
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWebhooksAsync_CancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _svc.GetWebhooksAsync(CompanyId, cts.Token));
    }
}
