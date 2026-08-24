// ============================================================================
// Audit item 9 — unit tests for the shared upload validator.
//
// Covers the three behaviours the audit calls out explicitly:
//   • a genuine, in-profile file passes and receives a server-generated name
//   • a spoofed extension (wrong magic bytes / mismatched declared MIME) is rejected
//   • an oversized file is rejected before the bytes are inspected
// plus the fail-closed paths (missing file, extension outside the profile).
// ============================================================================
using System.Text;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HRMS.Tests.Security;

public class UploadValidatorTests
{
    // ── Byte-level fixtures (real magic-byte prefixes) ────────────────────
    private static readonly byte[] PngHeader  = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    private static readonly byte[] PdfHeader  = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
    private static readonly byte[] ZipHeader  = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 };

    private static IFormFile File(
        string fileName, string contentType, byte[] header, long? reportedLength = null)
    {
        var body = new MemoryStream();
        body.Write(header, 0, header.Length);
        body.Write(Encoding.ASCII.GetBytes(new string('A', 512)));
        body.Position = 0;

        return new FakeFormFile(body, fileName, contentType, reportedLength ?? body.Length);
    }

    /// <summary>
    /// Minimal IFormFile whose reported Length can be decoupled from the actual
    /// stream, so the size gate can be tested without allocating megabytes.
    /// </summary>
    private sealed class FakeFormFile : IFormFile
    {
        private readonly Stream _stream;

        public FakeFormFile(Stream stream, string fileName, string contentType, long length)
        {
            _stream     = stream;
            FileName    = fileName;
            ContentType = contentType;
            Length      = length;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name => "file";
        public string FileName { get; }

        public void CopyTo(Stream target) => _stream.CopyTo(target);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => _stream.CopyToAsync(target, cancellationToken);
        public Stream OpenReadStream()
        {
            _stream.Position = 0;
            return _stream;
        }
    }

    // ── Happy paths ───────────────────────────────────────────────────────

    [Fact]
    public void Valid_png_passes_the_image_profile()
    {
        var result = UploadValidator.Validate(
            File("avatar.png", "image/png", PngHeader), UploadProfile.Image);

        Assert.True(result.IsValid, result.Error);
        Assert.Null(result.Error);
        Assert.Equal(".png", result.Extension);
        Assert.NotNull(result.SafeFileName);
        Assert.EndsWith(".png", result.SafeFileName);
        // The client-supplied name must never survive into the storage name.
        Assert.DoesNotContain("avatar", result.SafeFileName!);
    }

    [Fact]
    public void Valid_jpeg_and_pdf_pass_their_profiles()
    {
        Assert.True(UploadValidator
            .Validate(File("photo.jpg", "image/jpeg", JpegHeader), UploadProfile.Image).IsValid);
        Assert.True(UploadValidator
            .Validate(File("cv.pdf", "application/pdf", PdfHeader), UploadProfile.Resume).IsValid);
        Assert.True(UploadValidator
            .Validate(File("import.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ZipHeader), UploadProfile.Spreadsheet).IsValid);
    }

    [Fact]
    public void Two_uploads_never_collide_on_the_generated_name()
    {
        var a = UploadValidator.Validate(File("a.png", "image/png", PngHeader), UploadProfile.Image);
        var b = UploadValidator.Validate(File("a.png", "image/png", PngHeader), UploadProfile.Image);
        Assert.NotEqual(a.SafeFileName, b.SafeFileName);
    }

    // ── Spoofed extensions ────────────────────────────────────────────────

    [Fact]
    public void Rejects_executable_renamed_to_png()
    {
        // MZ header (Windows PE) announced as image/png with a .png extension.
        var mz = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var result = UploadValidator.Validate(
            File("payload.png", "image/png", mz), UploadProfile.Image);

        Assert.False(result.IsValid);
        Assert.Contains("does not match a valid '.png' file", result.Error);
    }

    [Fact]
    public void Rejects_pdf_bytes_declared_and_named_as_an_image()
    {
        var result = UploadValidator.Validate(
            File("scan.png", "image/png", PdfHeader), UploadProfile.Image);

        Assert.False(result.IsValid);
        Assert.Contains("does not match a valid", result.Error);
    }

    [Fact]
    public void Rejects_declared_mime_that_contradicts_the_extension()
    {
        // Real PNG bytes and a .png name, but the browser announces application/pdf.
        var result = UploadValidator.Validate(
            File("avatar.png", "application/pdf", PngHeader), UploadProfile.Image);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.Error);
    }

    [Fact]
    public void Rejects_extension_outside_the_profile_even_when_bytes_are_genuine()
    {
        // A real PDF is a valid Document upload but must not pass the Image profile.
        var result = UploadValidator.Validate(
            File("cv.pdf", "application/pdf", PdfHeader), UploadProfile.Image);

        Assert.False(result.IsValid);
        Assert.Contains("not allowed", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("shell.exe",  "application/octet-stream")]
    [InlineData("script.svg", "image/svg+xml")]
    [InlineData("archive.zip", "application/zip")]
    public void Rejects_dangerous_extensions(string name, string contentType)
    {
        var result = UploadValidator.Validate(
            File(name, contentType, PngHeader), UploadProfile.Image);
        Assert.False(result.IsValid);
    }

    // ── Size ceiling ──────────────────────────────────────────────────────

    [Fact]
    public void Rejects_file_larger_than_the_profile_ceiling()
    {
        // Image profile is 5 MB; report 6 MB while keeping the fixture tiny.
        var result = UploadValidator.Validate(
            File("big.png", "image/png", PngHeader, reportedLength: 6L * 1024 * 1024),
            UploadProfile.Image);

        Assert.False(result.IsValid);
        Assert.Contains("exceed", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Global_configuration_can_only_narrow_the_profile_ceiling()
    {
        // 3 MB file: allowed by the 5 MB Image profile, refused by a 2 MB global cap.
        var file = File("big.png", "image/png", PngHeader, reportedLength: 3L * 1024 * 1024);
        Assert.True(UploadValidator.Validate(file, UploadProfile.Image).IsValid);
        Assert.False(UploadValidator.Validate(file, UploadProfile.Image, globalMaxSizeMB: 2).IsValid);
    }

    // ── Presence / fail-closed ────────────────────────────────────────────

    [Fact]
    public void Rejects_missing_file_when_required()
    {
        var result = UploadValidator.Validate(null, UploadProfile.Image);
        Assert.False(result.IsValid);
        Assert.Contains("No file", result.Error);
    }

    [Fact]
    public void Accepts_missing_file_when_optional()
    {
        var result = UploadValidator.Validate(null, UploadProfile.Document, required: false);
        Assert.True(result.IsValid);
        Assert.Equal(string.Empty, result.SafeFileName);
    }

    [Fact]
    public void Rejects_empty_file()
    {
        var empty = new FakeFormFile(new MemoryStream(), "empty.png", "image/png", 0);
        Assert.False(UploadValidator.Validate(empty, UploadProfile.Image).IsValid);
    }

    // ── EnsureValid throwing surface (mapped to HTTP 400 by ExceptionMiddleware) ──

    [Fact]
    public void EnsureValid_throws_UploadValidationException_for_a_spoofed_file()
    {
        var mz = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var ex = Assert.Throws<UploadValidationException>(() =>
            UploadValidator.EnsureValid(File("payload.png", "image/png", mz), UploadProfile.Image));
        Assert.Contains("does not match a valid", ex.Message);
    }

    [Fact]
    public void EnsureValid_returns_the_safe_name_for_a_genuine_file()
    {
        var safeName = UploadValidator.EnsureValid(
            File("avatar.png", "image/png", PngHeader), UploadProfile.Image);
        Assert.EndsWith(".png", safeName);
    }

    // ── Profile resolution ────────────────────────────────────────────────

    [Theory]
    [InlineData("profile", "image")]
    [InlineData("logo",    "image")]
    [InlineData("resumes", "resume")]
    [InlineData("unknown-subfolder", "document")]
    [InlineData(null,      "document")]
    public void Subfolder_resolution_never_falls_back_to_allow_all(string? subfolder, string expected)
    {
        Assert.Equal(expected, UploadProfile.ForSubfolder(subfolder).Name);
    }

    [Fact]
    public void SanitizeToSafeName_strips_path_and_traversal_characters()
    {
        var name = UploadValidator.SanitizeToSafeName("../../.png");
        Assert.DoesNotContain("/", name);
        Assert.DoesNotContain("\\", name);
        Assert.EndsWith(".png", name);
    }
}
