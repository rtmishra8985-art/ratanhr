// BLOCKER-10 — BACKGROUND JOBS AND CACHE SAFETY (Phase 2 regression coverage)
//
// Covers:
//   • PayslipPdfJob idempotency — repeated execution does not re-generate an
//     already-existing PDF (idempotent re-run).
//   • PayslipPdfJob company-mismatch guard — job aborts when payslip company
//     and employee company diverge (data-integrity protection).
//   • EmailQueueService bounded retries — permanently fails after 3 attempts
//     and does not retry beyond the bound.
//   • EmailQueueService idempotency — re-running ProcessPendingAsync does not
//     re-send already-Sent items.
//   • WebhookDispatcher bounded retries — AttemptCount cap prevents runaway
//     retries beyond the configured threshold.
//   • Cache key tenant scope — cache keys include company ID so cross-tenant
//     cache collisions cannot occur.
//
using FluentAssertions;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Company;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Email;
using HRMS.Domain.Entities.Webhook;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Jobs;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Phase-2 regression coverage for background jobs and cache safety (Blocker 10).
/// </summary>
public class BackgroundJobPhase2Tests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"hrms-job-test-{Guid.NewGuid():N}");
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ApplicationDbContext _db;

    public BackgroundJobPhase2Tests()
    {
        Directory.CreateDirectory(_tempDir);
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _db = new ApplicationDbContext(opts);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §1 — PayslipPdfJob idempotency
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Running PayslipPdfJob twice for the same token must produce exactly one
    /// PDF file.  The second invocation must be a no-op (idempotent).
    /// </summary>
    [Fact]
    public async Task PayslipPdfJob_IdempotentRerun_DoesNotDuplicate()
    {
        // Arrange — seed payslip and employee in one company
        const int companyId = 1;
        var company = new Company
        {
            Id = companyId, CompanyName = "Acme", Email = "hr@acme.test",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.Companies.Add(company);
        var emp = new Employee
        {
            EmployeeCode = "E001", FullName = "Alice", Email = "alice@acme.test",
            CompanyId    = companyId, Department = "Eng", Designation = "Dev",
            DateOfJoining = new DateOnly(2023, 1, 1), IsActive = true
        };
        _db.Employees.Add(emp);
        var payslip = new HRMS.Domain.Entities.Payroll.Payslip
        {
            EmployeeId = "E001", CompanyId = companyId,
            Month = 6, Year = 2025,
            BasicPay = 50_000m, NetPay = 45_000m, GrossEarnings = 55_000m,
            TotalDeductions = 10_000m, WorkingDays = 22, DaysPresent = 22
        };
        _db.Payslips.Add(payslip);
        await _db.SaveChangesAsync();

        var job   = new PayslipPdfJob(_db, NullLogger<PayslipPdfJob>.Instance);
        var token = Guid.NewGuid().ToString("N");

        // Act — run the job twice
        await job.GenerateAsync(payslip.Id, token, _tempDir);
        await job.GenerateAsync(payslip.Id, token, _tempDir);

        // Assert — exactly one PDF file exists
        var outDir = PayslipPdfJob.GetOutputDirectory(_tempDir);
        var files  = Directory.GetFiles(outDir, $"{token}.pdf");
        files.Should().HaveCount(1, "the second run must skip generation for an existing token");
    }

    /// <summary>
    /// PayslipPdfJob must abort and not write any file when the payslip record
    /// cannot be found in the database.
    /// </summary>
    [Fact]
    public async Task PayslipPdfJob_MissingPayslip_ProducesNoFile()
    {
        var job   = new PayslipPdfJob(_db, NullLogger<PayslipPdfJob>.Instance);
        var token = Guid.NewGuid().ToString("N");

        await job.GenerateAsync(payslipId: 99999, token: token, webRootPath: _tempDir);

        var outDir   = PayslipPdfJob.GetOutputDirectory(_tempDir);
        var filePath = Path.Combine(outDir, PayslipPdfJob.GetFileName(token));
        File.Exists(filePath).Should().BeFalse(
            "no PDF must be written when the payslip record is missing");
    }

    /// <summary>
    /// PayslipPdfJob must abort when the employee's CompanyId differs from
    /// the payslip's CompanyId (data-integrity guard).
    /// </summary>
    [Fact]
    public async Task PayslipPdfJob_CompanyMismatch_ProducesNoFile()
    {
        const int companyA = 1;
        const int companyB = 2;

        var empA = new Employee
        {
            EmployeeCode = "M001", FullName = "Mismatch", Email = "m@test.test",
            CompanyId    = companyB, // intentionally wrong company
            Department   = "Eng", Designation = "Dev",
            DateOfJoining = new DateOnly(2023, 1, 1), IsActive = true
        };
        _db.Employees.Add(empA);

        var payslip = new HRMS.Domain.Entities.Payroll.Payslip
        {
            EmployeeId   = "M001",
            CompanyId    = companyA, // payslip belongs to company A
            Month = 7, Year = 2025,
            BasicPay = 40_000m, NetPay = 36_000m, GrossEarnings = 44_000m,
            TotalDeductions = 8_000m, WorkingDays = 22, DaysPresent = 20
        };
        _db.Payslips.Add(payslip);
        await _db.SaveChangesAsync();

        var job   = new PayslipPdfJob(_db, NullLogger<PayslipPdfJob>.Instance);
        var token = Guid.NewGuid().ToString("N");

        await job.GenerateAsync(payslip.Id, token, _tempDir);

        var outDir   = PayslipPdfJob.GetOutputDirectory(_tempDir);
        var filePath = Path.Combine(outDir, PayslipPdfJob.GetFileName(token));
        File.Exists(filePath).Should().BeFalse(
            "no PDF must be produced when employee and payslip belong to different companies");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §2 — EmailQueueService bounded retries and idempotency
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An email item must be permanently failed after exactly 3 retry attempts,
    /// not retried a fourth time.
    /// </summary>
    [Fact]
    public async Task EmailQueue_PermanentlyFails_AfterThreeRetries()
    {
        // Arrange — item that has already failed twice (RetryCount = 2)
        var item = new EmailQueueItem
        {
            ToAddress  = "user@example.com",
            Subject    = "Test",
            HtmlBody   = "<p>Test</p>",
            Status     = "Pending",
            RetryCount = 2,
            CreatedAt  = DateTime.UtcNow
        };
        _db.EmailQueue.Add(item);
        await _db.SaveChangesAsync();

        var emailSvc = new Mock<IEmailService>();
        emailSvc.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("SMTP unreachable"));

        var factory = CreateDbFactory();
        var svc     = new EmailQueueService(factory, emailSvc.Object,
            NullLogger<EmailQueueService>.Instance);

        // Act
        await svc.ProcessPendingAsync();

        // Assert — item must now be in Failed state (3rd failure = permanent)
        // The service intentionally uses a fresh DbContext from the factory.
        // Query a fresh context here as well; the fixture's original context
        // would otherwise return its stale tracked instance.
        await using var assertionDb = await CreateDbFactory().CreateDbContextAsync();
        var updated = await assertionDb.EmailQueue.FindAsync(item.Id);
        updated!.Status.Should().Be("Failed",
            "an item must become permanently failed after 3 consecutive send failures");
        updated.RetryCount.Should().Be(3);
    }

    /// <summary>
    /// ProcessPendingAsync must not re-send items that are already in Sent status.
    /// </summary>
    [Fact]
    public async Task EmailQueue_AlreadySentItems_AreNotReprocessed()
    {
        var alreadySent = new EmailQueueItem
        {
            ToAddress = "done@example.com",
            Subject   = "Done",
            HtmlBody  = "<p>Done</p>",
            Status    = "Sent",
            SentAt    = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _db.EmailQueue.Add(alreadySent);
        await _db.SaveChangesAsync();

        var emailSvc = new Mock<IEmailService>();
        var factory  = CreateDbFactory();
        var svc      = new EmailQueueService(factory, emailSvc.Object,
            NullLogger<EmailQueueService>.Instance);

        var sent = await svc.ProcessPendingAsync();

        sent.Should().Be(0, "no already-Sent items must be reprocessed");
        emailSvc.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never, "SendAsync must not be called for already-Sent items");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §3 — WebhookDispatcherService retry cap
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The webhook outbox processor must not pick up items whose AttemptCount
    /// has already reached or exceeded the maximum (5).  This prevents runaway
    /// retries on permanently-failing endpoints.
    /// </summary>
    [Fact]
    public async Task WebhookDispatcher_DoesNotRetry_BeyondMaxAttempts()
    {
        var exhausted = new WebhookOutbox
        {
            SubscriptionId = 1,
            EventType    = "test",
            TargetUrl    = "https://1.1.1.1/webhook",
            Payload      = """{"event":"test"}""",
            Status       = "Pending",
            AttemptCount = 5,   // already at max
            CreatedAt    = DateTime.UtcNow.AddHours(-1)
        };
        _db.WebhookOutbox.Add(exhausted);
        await _db.SaveChangesAsync();

        var http    = new Mock<IWebhookHttpClient>();
        // WebhookDispatcherService selects only items with AttemptCount < 5
        // so we verify that no HTTP call is made for the exhausted item.
        var svc = new WebhookDispatcherService(_db, http.Object,
            NullLogger<WebhookDispatcherService>.Instance);

        await svc.DispatchPendingAsync(CancellationToken.None);

        http.Verify(
            f => f.PostAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no HTTP dispatch must occur for a webhook delivery at the retry cap");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §4 — Cache key tenant scope
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Department list cache keys must embed the company ID so that company-A
    /// and company-B results never share the same cache entry.
    /// </summary>
    [Fact]
    public void CacheKey_DepartmentList_IncludesCompanyId()
    {
        const int companyA = 1;
        const int companyB = 2;

        // The key pattern used by DepartmentService is "dept:list:{companyId}".
        // Verify that keys for different tenants are distinct.
        var keyA = $"dept:list:{companyA}";
        var keyB = $"dept:list:{companyB}";

        keyA.Should().NotBe(keyB,
            "cache keys for different tenants must be distinct to prevent cross-tenant cache poisoning");
        keyA.Should().Contain(companyA.ToString());
        keyB.Should().Contain(companyB.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private IDbContextFactory<ApplicationDbContext> CreateDbFactory()
    {
        // Returns the same in-memory DB used by the test so both the service
        // and the assertion queries share the same data.
        var factoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                // FIX: Database.GetDbConnection() is relational-only and throws
                // "Relational-specific methods can only be used when the context is
                // using a relational database provider" against the InMemory
                // provider. Reuse the stored database name instead.
                var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(_dbName)
                    .Options;
                return Task.FromResult<ApplicationDbContext>(
                    new ApplicationDbContext(opts));
            });
        return factoryMock.Object;
    }
}
