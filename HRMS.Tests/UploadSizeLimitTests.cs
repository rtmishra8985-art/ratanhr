// New file — provides unit-test coverage for the 30 MB upload size limit and related
// file validation behaviour in FileStorageService. Tests cover:
//   • Files over MaxFileSizeMB are rejected with FileUploadValidationException
//   • Files exactly at the limit are accepted
//   • Disallowed extensions are rejected
//   • Valid magic bytes pass; mismatched magic bytes fail (mime/extension spoofing)
//   • Path traversal in Delete() is silently ignored
//
// FileStorageService is the single choke-point for all file uploads in the application.
// Controllers annotate [RequestSizeLimit(30 * 1024 * 1024)] to enforce the HTTP layer
// limit; FileStorageService enforces the application-layer limit independently.
using HRMS.Infrastructure.FileStorage;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for <see cref="FileStorageService"/> file validation: size limits, extension
/// allow-listing, magic-byte content verification, and path traversal guards.
/// </summary>
/// <remarks>
/// Implements <see cref="IDisposable"/> to clean up any temporary directories created
/// by successful <c>SaveAsync</c> calls. Without cleanup, repeated test runs accumulate
/// stale files in <see cref="Path.GetTempPath"/>; in sandboxed CI environments write
/// access to the temp directory may also be restricted after the test process exits.
/// </remarks>
public class UploadSizeLimitTests : IDisposable
{
    // Track every subfolder name that a successful SaveAsync may have created
    // under Path.GetTempPath() so Dispose() can remove them unconditionally.
    private readonly List<string> _tempSubfolders =
    [
        "test",          // used by most tests
        "uploads-test",  // used by ExactlyAtMaxSize / OneByteBelowLimit
    ];

    /// <inheritdoc/>
    public void Dispose()
    {
        // Remove every temporary directory that could have been created by SaveAsync.
        // Errors are swallowed — a best-effort cleanup must not fail the test run.
        var root = Path.GetTempPath();
        foreach (var sub in _tempSubfolders)
        {
            var dir = Path.Combine(root, sub);
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* swallow — temp cleanup failures are non-fatal */ }
        }
    }

    // ── Factory helpers ────────────────────────────────────────────────────

    private static FileStorageService CreateService(int maxMb = 30,
        string[]? extensions = null) =>
        new(Path.GetTempPath(),
            Options.Create(new FileUploadOptions
            {
                MaxFileSizeMB = maxMb,
                AllowedExtensions = extensions
                    ?? [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx"]
            }));

    private static readonly UploadProfile ThirtyMegabyteImageProfile = new()
    {
        Name = "test-image",
        AllowedExtensions = [".jpg"],
        MaxSizeMB = 30
    };

    /// <summary>
    /// Creates a FormFile whose <c>Length</c> property returns
    /// <paramref name="reportedLength"/> while the backing stream contains a small
    /// header that satisfies the magic-byte check for the given content type.
    /// This lets tests exercise the size-check path without allocating huge arrays.
    /// </summary>
    private static IFormFile CreateFile(
        string filename,
        long reportedLength,
        string contentType,
        byte[]? content = null)
    {
        // Default content: minimal valid JPEG header (FF D8 FF + filler)
        content ??= [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x00, 0x00];
        var stream = new MemoryStream(content);
        // FormFile(baseStream, offset, length, name, fileName)
        // Setting length to reportedLength lets us exercise the size check without
        // materialising a multi-MB byte array in the test process.
        return new FormFile(stream, 0, reportedLength, "file", filename)
        {
            Headers     = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    // ── 30 MB size limit ───────────────────────────────────────────────────

    [Fact]
    public async Task UploadFile_ExceedsMaxSize_ThrowsFileUploadValidationException()
    {
        var svc  = CreateService(maxMb: 30);
        long overLimit = 31L * 1024 * 1024; // 31 MB > 30 MB limit

        var file = CreateFile("photo.jpg", overLimit, "image/jpeg");

        // Effective limit = Min(profile.MaxSizeMB, options.MaxFileSizeMB). The bare
        // "test" subfolder resolves via UploadProfile.ForSubfolder to the Document
        // profile (10 MB), which would silently cap the effective limit at 10 MB and
        // make the "references 30" assertion below false regardless of the 30 MB
        // service ceiling. Pass the 30 MB profile explicitly so the scenario this
        // test names — a 30 MB configured limit — is the one actually exercised.
        var ex = await Assert.ThrowsAsync<FileUploadValidationException>(
            () => svc.SaveAsync(file, "test", ThirtyMegabyteImageProfile));

        Assert.Contains("30", ex.Message); // message references the configured limit
    }

    [Fact]
    public async Task UploadFile_ExactlyAtMaxSize_IsAccepted()
    {
        var svc = CreateService(maxMb: 30);
        // A JPEG file that is exactly 30 MB: valid magic bytes, reported length = 30 MB
        long exactly30Mb = 30L * 1024 * 1024;

        var file = CreateFile("photo.jpg", exactly30Mb, "image/jpeg");

        // Must not throw — exactly-at-limit is allowed.
        var ex = await Record.ExceptionAsync(() =>
            svc.SaveAsync(file, "uploads-test", ThirtyMegabyteImageProfile));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UploadFile_OneByteBelowLimit_IsAccepted()
    {
        var svc = CreateService(maxMb: 1); // 1 MB limit for speed
        long justUnder = 1L * 1024 * 1024 - 1;

        var file = CreateFile("photo.jpg", justUnder, "image/jpeg");

        var ex = await Record.ExceptionAsync(() => svc.SaveAsync(file, "uploads-test"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UploadFile_OneBytOverLimit_ThrowsFileUploadValidationException()
    {
        var svc = CreateService(maxMb: 1);
        long justOver = 1L * 1024 * 1024 + 1;

        var file = CreateFile("photo.jpg", justOver, "image/jpeg");

        await Assert.ThrowsAsync<FileUploadValidationException>(() => svc.SaveAsync(file, "test"));
    }

    // ── Null / empty file ─────────────────────────────────────────────────

    [Fact]
    public async Task UploadFile_NullFile_ReturnsNull()
    {
        var svc    = CreateService();
        var result = await svc.SaveAsync(null, "test");
        Assert.Null(result);
    }

    [Fact]
    public async Task UploadFile_ZeroLengthFile_ReturnsNull()
    {
        var svc  = CreateService();
        var file = CreateFile("empty.jpg", 0, "image/jpeg", []);
        var result = await svc.SaveAsync(file, "test");
        Assert.Null(result);
    }

    // ── Extension allow-list ───────────────────────────────────────────────

    [Fact]
    public async Task UploadFile_DisallowedExtension_ThrowsFileUploadValidationException()
    {
        var svc  = CreateService();
        var file = CreateFile("script.exe", 100, "application/octet-stream",
            [0x4D, 0x5A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // MZ header

        var ex = await Assert.ThrowsAsync<FileUploadValidationException>(
            () => svc.SaveAsync(file, "test"));

        Assert.Contains(".exe", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFile_AllowedExtension_DoesNotThrowOnExtensionCheck()
    {
        var svc  = CreateService(extensions: [".pdf"]);
        // Valid PDF magic bytes: %PDF = 0x25 0x50 0x44 0x46
        var file = CreateFile("document.pdf", 512,
            "application/pdf",
            [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]);

        var ex = await Record.ExceptionAsync(() => svc.SaveAsync(file, "test"));
        Assert.Null(ex);
    }

    // ── Magic-byte (MIME) validation ───────────────────────────────────────

    [Fact]
    public async Task UploadFile_JpegWithWrongMagicBytes_ThrowsFileUploadValidationException()
    {
        var svc = CreateService();
        // File claims to be JPEG but starts with PNG bytes — spoofing attempt.
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var file = CreateFile("photo.jpg", pngBytes.Length, "image/jpeg", pngBytes);

        await Assert.ThrowsAsync<FileUploadValidationException>(
            () => svc.SaveAsync(file, "test"));
    }

    [Fact]
    public async Task UploadFile_PdfWithCorrectMagicBytes_DoesNotThrow()
    {
        var svc = CreateService();
        // %PDF-1.4
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
        var file = CreateFile("doc.pdf", pdfBytes.Length, "application/pdf", pdfBytes);

        var ex = await Record.ExceptionAsync(() => svc.SaveAsync(file, "test"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UploadFile_PngWithCorrectMagicBytes_DoesNotThrow()
    {
        var svc = CreateService();
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var file = CreateFile("image.png", pngBytes.Length, "image/png", pngBytes);

        var ex = await Record.ExceptionAsync(() => svc.SaveAsync(file, "test"));
        Assert.Null(ex);
    }

    // ── Path traversal in Delete() ─────────────────────────────────────────

    [Fact]
    public void Delete_PathTraversalAttempt_IsSilentlyIgnored()
    {
        var svc = CreateService();
        // A crafted path that would escape the uploads directory.
        // FileStorageService must silently ignore it (no exception, no file deletion
        // outside the uploads root).
        var ex = Record.Exception(() => svc.Delete("../../etc/passwd"));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_AbsolutePathTraversal_IsSilentlyIgnored()
    {
        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Delete("/etc/passwd"));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_NullPath_IsSilentlyIgnored()
    {
        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Delete(null));
        Assert.Null(ex);
    }

    [Fact]
    public void Delete_EmptyPath_IsSilentlyIgnored()
    {
        var svc = CreateService();
        var ex  = Record.Exception(() => svc.Delete(string.Empty));
        Assert.Null(ex);
    }

    // ── Configurable limit parity with [RequestSizeLimit] ─────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(100)]
    public async Task UploadFile_ExceedsConfiguredLimit_AlwaysThrows(int maxMb)
    {
        var svc  = CreateService(maxMb: maxMb);
        long over = (long)(maxMb + 1) * 1024 * 1024;
        var file = CreateFile("photo.jpg", over, "image/jpeg");

        await Assert.ThrowsAsync<FileUploadValidationException>(
            () => svc.SaveAsync(file, "test"));
    }
}
