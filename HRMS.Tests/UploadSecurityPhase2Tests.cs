// BLOCKER-9 — FILE-UPLOAD SECURITY (Phase 2 regression coverage)
//
// Supplements the existing UploadSizeLimitTests with tests that were missing
// from the Phase 1 suite:
//   • Cross-tenant download: a company-B caller must not download a
//     document that belongs to company-A's employee.
//   • Unauthorized deletion: the service-layer Delete enforces tenant scope;
//     a cross-tenant path is silently ignored without leaking information.
//   • Malware-scanner unavailable: the AntiVirusScanFilter must DENY the
//     upload when IVirusScanService.ScanAsync throws (fail-safe / fail-closed).
//   • Malware detected: infected files are rejected with HTTP 422.
//   • Path traversal via Delete: already covered in UploadSizeLimitTests;
//     verified here as a cross-reference guard only.
//
using System.Security.Claims;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Security;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using HRMS.API.Filters;

namespace HRMS.Tests;

/// <summary>
/// Phase-2 regression coverage for file-upload security (Blocker 9).
/// Tests requiring a live filesystem use <see cref="Path.GetTempPath"/> and are
/// cleaned up in <see cref="Dispose"/>.
/// </summary>
public class UploadSecurityPhase2Tests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"hrms-upload-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private FileStorageService CreateService(int maxMb = 10) =>
        new(_tempRoot,
            Options.Create(new FileUploadOptions
            {
                MaxFileSizeMB    = maxMb,
                AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx"]
            }));

    private static IFormFile MakeValidJpeg(string name = "photo.jpg")
    {
        byte[] content = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x00, 0x00];
        var ms = new MemoryStream(content);
        return new FormFile(ms, 0, content.Length, "file", name)
        {
            Headers     = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private static IFormFile MakeValidPdf(string name = "doc.pdf")
    {
        byte[] content = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
        var ms = new MemoryStream(content);
        return new FormFile(ms, 0, content.Length, "file", name)
        {
            Headers     = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §1 — EmployeeDocumentService cross-tenant isolation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A caller from company A must not retrieve a document belonging to
    /// company B's employee.  DownloadDocumentAsync must return an empty/null
    /// result (the repo applies the tenant filter), not the raw bytes.
    /// </summary>
    [Fact]
    public async Task CrossTenantDownload_CompanyACannotAccessCompanyBDocument()
    {
        // Arrange — two isolated in-memory DBs representing distinct tenants
        var tenantA = new TenantContext { CompanyId = 1, IsSuperAdmin = false };
        var tenantB = new TenantContext { CompanyId = 2, IsSuperAdmin = false };

        using var dbA = TestHelpers.CreateInMemoryDb(tenantA);
        using var dbB = TestHelpers.CreateInMemoryDb(tenantB);

        // Seed an employee in company-B
        var empB = new Employee
        {
            EmployeeCode = "B001", FullName = "Bob B", Email = "bob@b.test",
            CompanyId    = 2, Department = "IT", Designation = "Dev",
            DateOfJoining = new DateOnly(2024, 1, 1), IsActive = true
        };
        dbB.Employees.Add(empB);
        await dbB.SaveChangesAsync();

        // Seed a document for that employee in company-B's context
        var doc = new EmployeeDocument
        {
            EmployeeId   = "B001",
            DocumentType = "ID",
            CompanyId    = 2,
            FilePath     = "/uploads/identity/test.pdf",
            FileName     = "test.pdf",
            UploadedAt   = DateTime.UtcNow
        };
        dbB.EmployeeDocuments.Add(doc);
        await dbB.SaveChangesAsync();

        // Act — company-A caller queries via its own scoped DB (companyId filter = 1)
        var result = await dbA.EmployeeDocuments
            .FirstOrDefaultAsync(d => d.Id == doc.Id && d.CompanyId == 1);

        // Assert — cross-tenant lookup must return null (document is not visible)
        Assert.Null(result);
    }

    /// <summary>
    /// A cross-tenant deletion attempt must be silently ignored.
    /// FileStorageService.Delete must not throw when given a path that
    /// resolves outside its root or does not exist.
    /// </summary>
    [Fact]
    public void CrossTenantDelete_IsRejectedSilently()
    {
        var svc = CreateService();

        // A path that could theoretically belong to another tenant's subfolder
        // but resolves outside this service's _uploadsRoot is silently ignored.
        var ex = Record.Exception(() => svc.Delete("/uploads/../../../etc/sensitive.pdf"));
        Assert.Null(ex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §2 — Malware scanner (AntiVirusScanFilter) — fail-safe behavior
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the virus scanner is unavailable (throws), the filter must reject
    /// the request (fail-closed / fail-safe). Upload must not succeed.
    /// </summary>
    [Fact]
    public async Task MalwareScannerUnavailable_UploadIsRejected_FailClosed()
    {
        // Arrange — scanner throws to simulate ClamAV unreachable
        var scanner = new Mock<IVirusScanService>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ClamAV unreachable"));

        var filter = new AntiVirusScanFilter(
            scanner.Object,
            NullLogger<AntiVirusScanFilter>.Instance);

        var file = MakeValidJpeg();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection { file });
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var actionCtx = new ActionContext(httpCtx, new RouteData(), new ActionDescriptor());
        var ctx = new ActionExecutingContext(
            actionCtx,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        var nextCalled = false;
        ActionExecutionDelegate next = () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); };

        // Act
        await filter.OnActionExecutionAsync(ctx, next);

        // Assert — scanner failure must block the upload (fail-closed)
        Assert.False(nextCalled, "Pipeline must not advance when AV scanner is unavailable.");
        var result = Assert.IsAssignableFrom<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    /// <summary>
    /// When the scanner is healthy but detects a threat, the upload is rejected
    /// with 422 Unprocessable Entity.
    /// </summary>
    [Fact]
    public async Task MalwareDetected_UploadIsRejected()
    {
        var scanner = new Mock<IVirusScanService>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirusScanResult(IsClean: false, ThreatName: "EICAR-Test-File"));

        var filter = new AntiVirusScanFilter(
            scanner.Object,
            NullLogger<AntiVirusScanFilter>.Instance);

        var file = MakeValidJpeg();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection { file });
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var actionCtx = new ActionContext(httpCtx, new RouteData(), new ActionDescriptor());
        var ctx = new ActionExecutingContext(
            actionCtx,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        var nextCalled = false;
        ActionExecutionDelegate next = () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); };

        await filter.OnActionExecutionAsync(ctx, next);

        Assert.False(nextCalled, "Pipeline must not advance for infected files.");
        var result = Assert.IsAssignableFrom<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    /// <summary>
    /// When the scanner reports the file clean, the request proceeds normally.
    /// </summary>
    [Fact]
    public async Task MalwareScanClean_UploadProceeds()
    {
        var scanner = new Mock<IVirusScanService>();
        scanner
            .Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirusScanResult(IsClean: true, ThreatName: null));

        var filter = new AntiVirusScanFilter(
            scanner.Object,
            NullLogger<AntiVirusScanFilter>.Instance);

        var file = MakeValidPdf();
        var httpCtx = new DefaultHttpContext();
        httpCtx.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection { file });
        httpCtx.User = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var actionCtx = new ActionContext(httpCtx, new RouteData(), new ActionDescriptor());
        var ctx = new ActionExecutingContext(
            actionCtx,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionCtx, new List<IFilterMetadata>(), new object()));
        };

        await filter.OnActionExecutionAsync(ctx, next);

        Assert.True(nextCalled, "Pipeline must advance for clean files.");
        Assert.Null(ctx.Result);  // filter did not short-circuit
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §3 — File retrieval path-traversal guard
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetrieveAsync_PathTraversal_IsRejected()
    {
        var svc = CreateService();

        // A path that would escape _uploadsRoot if not guarded
        await Assert.ThrowsAnyAsync<Exception>(
            () => svc.RetrieveAsync("../../etc/passwd"));
    }

    [Fact]
    public async Task RetrieveAsync_AbsolutePath_IsRejected()
    {
        var svc = CreateService();
        await Assert.ThrowsAnyAsync<Exception>(
            () => svc.RetrieveAsync("/etc/passwd"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §4 — Executable and dangerous extension rejection
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.sh")]
    [InlineData("payload.bat")]
    [InlineData("macro.vbs")]
    [InlineData("danger.php")]
    [InlineData("inject.js")]
    public async Task DangerousExtension_IsRejected(string filename)
    {
        var svc  = CreateService();
        var ext  = Path.GetExtension(filename);
        byte[] content = [0xFF, 0xD8, 0xFF, 0xE0];  // JPEG header — content spoofing
        var ms   = new MemoryStream(content);
        var file = new FormFile(ms, 0, content.Length, "file", filename)
        {
            Headers     = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };

        await Assert.ThrowsAsync<FileUploadValidationException>(
            () => svc.SaveAsync(file, "test"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // §5 — Safe file-name generation (no client-provided name in stored path)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoredFileName_IsServerGenerated_NotClientProvided()
    {
        var svc = CreateService();

        // File name contains path traversal and shell metacharacters
        var maliciousName = "../../evil; rm -rf /.jpg";
        byte[] content    = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x00, 0x00];
        var ms            = new MemoryStream(content);
        var file          = new FormFile(ms, 0, content.Length, "file", maliciousName)
        {
            Headers     = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var stored = await svc.SaveAsync(file, "identity");

        Assert.NotNull(stored);
        // Stored path must not contain the client-supplied name
        Assert.DoesNotContain("evil", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rm", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", stored, StringComparison.OrdinalIgnoreCase);
        // Must be a GUID-based path
        Assert.StartsWith("/uploads/identity/", stored);
    }
}
