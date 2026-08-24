// ============================================================================
// Audit item 9 — SHARED UPLOAD VALIDATOR
//
// Single, server-side source of truth for every IFormFile that enters the API.
// Replaces the per-controller ad-hoc checks (Content-Type string comparisons,
// hand-rolled size limits, client-supplied filenames) that previously differed
// between Profile photo, Attendance Excel import, EmployeeDocument, Logo,
// Recruitment resume and Expense receipt paths.
//
// Five gates, applied in order and failing closed:
//   1. Presence / non-empty
//   2. Size ceiling (profile ceiling, further clamped by FileUpload:MaxFileSizeMB)
//   3. Extension allow-list (per profile)
//   4. Declared MIME must be allow-listed AND agree with the extension
//   5. Magic-byte signature of the actual bytes must match the extension
//
// On success the caller receives a server-generated, GUID-based SafeFileName;
// the client-supplied name is never used to build a path.
//
// Callers:
//   • FileStorageService.SaveAsync  — covers profile photo, logo, resumes,
//     employee documents, appreciation attachments, expense receipts,
//     employee onboarding document collection.
//   • AttendanceController.UploadExcel — the one path that does not persist
//     through FileStorageService and therefore validates explicitly.
// ============================================================================
using Microsoft.AspNetCore.Http;

namespace HRMS.Infrastructure.Security;

/// <summary>
/// Thrown when an uploaded file fails validation. Controllers translate this to
/// HTTP 400 with <see cref="Exception.Message"/> as the user-facing reason.
/// </summary>
public sealed class UploadValidationException : Exception
{
    public UploadValidationException(string message) : base(message) { }
}

/// <summary>
/// Declarative description of what a particular upload endpoint accepts.
/// Profiles are intentionally narrow: an endpoint that only needs images must
/// not accept .docx just because the global allow-list contains it.
/// </summary>
public sealed class UploadProfile
{
    public required string Name { get; init; }

    /// <summary>Lower-case extensions including the leading dot.</summary>
    public required string[] AllowedExtensions { get; init; }

    /// <summary>Profile-specific size ceiling in megabytes.</summary>
    public required int MaxSizeMB { get; init; }

    // ── Presets ───────────────────────────────────────────────────────────

    /// <summary>Profile photos, company logos, appreciation images.</summary>
    public static readonly UploadProfile Image = new()
    {
        Name = "image",
        AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" },
        MaxSizeMB = 5,
    };

    /// <summary>Attendance Excel import — spreadsheets only.</summary>
    public static readonly UploadProfile Spreadsheet = new()
    {
        Name = "spreadsheet",
        AllowedExtensions = new[] { ".xlsx", ".xls" },
        MaxSizeMB = 10,
    };

    /// <summary>Recruitment resumes — documents only, never images or archives.</summary>
    public static readonly UploadProfile Resume = new()
    {
        Name = "resume",
        AllowedExtensions = new[] { ".pdf", ".doc", ".docx" },
        MaxSizeMB = 10,
    };

    /// <summary>Employee documents, expense receipts — scans or photos of paperwork.</summary>
    public static readonly UploadProfile Document = new()
    {
        Name = "document",
        AllowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" },
        MaxSizeMB = 10,
    };

    /// <summary>
    /// Resolves the profile for a storage subfolder used by
    /// <c>FileStorageService.SaveAsync</c>. Unknown subfolders fall back to
    /// <see cref="Document"/> (the most restrictive superset), never to "allow all".
    /// </summary>
    public static UploadProfile ForSubfolder(string? subfolder) => (subfolder ?? string.Empty).ToLowerInvariant() switch
    {
        "profile" or "photo" or "logo" or "appreciation" => Image,
        "resumes"                                        => Resume,
        _                                                => Document,
    };
}

/// <summary>
/// Result of a validation pass. <see cref="IsValid"/> false always carries a
/// non-null <see cref="Error"/>; true always carries a non-null <see cref="SafeFileName"/>.
/// </summary>
public sealed class UploadValidationResult
{
    public bool IsValid { get; private init; }
    public string? Error { get; private init; }
    public string? SafeFileName { get; private init; }
    public string? Extension { get; private init; }

    public static UploadValidationResult Fail(string error) => new() { IsValid = false, Error = error };

    public static UploadValidationResult Success(string safeFileName, string extension) =>
        new() { IsValid = true, SafeFileName = safeFileName, Extension = extension };

    /// <summary>Throws <see cref="UploadValidationException"/> when invalid; otherwise returns self.</summary>
    public UploadValidationResult EnsureValid()
    {
        if (!IsValid) throw new UploadValidationException(Error!);
        return this;
    }
}

/// <summary>
/// Stateless upload validator. Static by design so that service-layer and
/// seed/background paths outside the request pipeline cannot bypass it.
/// </summary>
public static class UploadValidator
{
    /// <summary>
    /// Extension → acceptable declared MIME types. Used for gate 4: the browser's
    /// Content-Type must be plausible for the extension. A file called "cv.pdf"
    /// announced as "image/png" is rejected outright.
    /// </summary>
    private static readonly Dictionary<string, string[]> ExtensionMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"]  = new[] { "image/jpeg", "image/jpg", "image/pjpeg" },
        [".jpeg"] = new[] { "image/jpeg", "image/jpg", "image/pjpeg" },
        [".png"]  = new[] { "image/png" },
        [".webp"] = new[] { "image/webp" },
        [".gif"]  = new[] { "image/gif" },
        [".pdf"]  = new[] { "application/pdf" },
        [".doc"]  = new[] { "application/msword", "application/vnd.ms-office" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".xls"]  = new[] { "application/vnd.ms-excel", "application/vnd.ms-office" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
    };

    /// <summary>
    /// Extension → accepted leading byte signatures (gate 5). Every extension a
    /// profile can allow MUST appear here, otherwise validation fails closed.
    /// </summary>
    private static readonly Dictionary<string, byte[][]> ExtensionSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"]  = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"]  = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } },              // RIFF
        [".gif"]  = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } },              // GIF8
        [".pdf"]  = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },              // %PDF
        [".doc"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },              // OLE2
        [".xls"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },              // OLE2
        [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 },                // ZIP / OOXML
                            new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                            new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
        [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                            new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                            new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
    };

    /// <summary>Number of leading bytes read for the signature check.</summary>
    public const int HeaderBytes = 8;

    /// <summary>
    /// Validates <paramref name="file"/> against <paramref name="profile"/>.
    /// </summary>
    /// <param name="file">The uploaded file; null/empty fails unless <paramref name="required"/> is false.</param>
    /// <param name="profile">Endpoint-specific accept rules.</param>
    /// <param name="globalMaxSizeMB">
    /// Deployment-wide ceiling from FileUpload:MaxFileSizeMB. The effective limit is
    /// the smaller of this and <see cref="UploadProfile.MaxSizeMB"/>.
    /// </param>
    /// <param name="globalAllowedExtensions">
    /// Deployment-wide extension allow-list. When non-empty the effective allow-list
    /// is the intersection with the profile's list, so configuration can only narrow.
    /// </param>
    /// <param name="required">When false, a null/empty file is accepted as "no upload".</param>
    public static UploadValidationResult Validate(
        IFormFile? file,
        UploadProfile profile,
        int? globalMaxSizeMB = null,
        IEnumerable<string>? globalAllowedExtensions = null,
        bool required = true)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // ── Gate 1: presence ─────────────────────────────────────────────
        if (file is null || file.Length == 0)
        {
            return required
                ? UploadValidationResult.Fail("No file was provided.")
                : UploadValidationResult.Success(string.Empty, string.Empty);
        }

        // ── Gate 2: size ─────────────────────────────────────────────────
        var effectiveMaxMB = globalMaxSizeMB is > 0
            ? Math.Min(profile.MaxSizeMB, globalMaxSizeMB.Value)
            : profile.MaxSizeMB;
        var maxBytes = (long)effectiveMaxMB * 1024 * 1024;
        if (file.Length > maxBytes)
            return UploadValidationResult.Fail(
                $"File exceeds the maximum allowed size of {effectiveMaxMB} MB.");

        // ── Gate 3: extension allow-list ─────────────────────────────────
        var rawName = file.FileName ?? string.Empty;
        // Strip any directory component a client may have smuggled in
        // ("../../etc/passwd", "C:\\evil\\x.png") before reading the extension.
        var leafName = Path.GetFileName(rawName.Replace('\\', '/'));
        var ext = Path.GetExtension(leafName).ToLowerInvariant();

        if (string.IsNullOrEmpty(ext))
            return UploadValidationResult.Fail("The file must have a recognised file extension.");

        var allowed = profile.AllowedExtensions;
        var globalList = globalAllowedExtensions?
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .ToArray();
        if (globalList is { Length: > 0 })
            allowed = allowed.Where(e => globalList.Contains(e)).ToArray();

        if (allowed.Length == 0)
            return UploadValidationResult.Fail(
                "File uploads are not permitted by the current server configuration.");

        if (!allowed.Contains(ext))
            return UploadValidationResult.Fail(
                $"File type '{ext}' is not allowed here. Permitted types: {string.Join(", ", allowed)}.");

        // ── Gate 4: declared MIME must agree with the extension ──────────
        var declared = (file.ContentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (ExtensionMimeMap.TryGetValue(ext, out var acceptableMimes))
        {
            if (string.IsNullOrEmpty(declared))
                return UploadValidationResult.Fail("The upload is missing a content type.");
            if (!acceptableMimes.Contains(declared, StringComparer.OrdinalIgnoreCase))
                return UploadValidationResult.Fail(
                    $"The declared content type '{declared}' does not match the '{ext}' file extension.");
        }

        // ── Gate 5: magic-byte signature of the real bytes ───────────────
        if (!ExtensionSignatures.TryGetValue(ext, out var signatures))
            return UploadValidationResult.Fail(
                $"File type '{ext}' cannot be content-verified and is therefore rejected.");

        byte[] header;
        try
        {
            using var stream = file.OpenReadStream();
            header = ReadHeader(stream);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
        {
            return UploadValidationResult.Fail("The uploaded file could not be read.");
        }

        if (!MatchesAny(header, signatures))
            return UploadValidationResult.Fail(
                $"The file content does not match a valid '{ext}' file. " +
                "Renaming a file to change its extension is not permitted.");

        // ── Success: server-generated safe name, client name discarded ───
        return UploadValidationResult.Success($"{Guid.NewGuid():N}{ext}", ext);
    }

    /// <summary>
    /// Convenience overload used by controllers: validates and throws
    /// <see cref="UploadValidationException"/> on failure.
    /// </summary>
    public static string EnsureValid(
        IFormFile? file,
        UploadProfile profile,
        int? globalMaxSizeMB = null,
        IEnumerable<string>? globalAllowedExtensions = null)
        => Validate(file, profile, globalMaxSizeMB, globalAllowedExtensions).EnsureValid().SafeFileName!;

    /// <summary>
    /// Server-generated storage name for an already-validated extension.
    /// Never derives any part of the name from client input.
    /// </summary>
    public static string SanitizeToSafeName(string extension)
    {
        var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && !ext.StartsWith('.')) ext = "." + ext;
        // Defence in depth: only [a-z0-9.] survives into a filesystem path.
        ext = new string(ext.Where(c => char.IsAsciiLetterOrDigit(c) || c == '.').ToArray());
        return $"{Guid.NewGuid():N}{ext}";
    }

    private static byte[] ReadHeader(Stream stream)
    {
        var buffer = new byte[HeaderBytes];
        var total = 0;
        while (total < HeaderBytes)
        {
            var read = stream.Read(buffer, total, HeaderBytes - total);
            if (read <= 0) break;
            total += read;
        }
        if (stream.CanSeek) stream.Position = 0;
        return total == HeaderBytes ? buffer : buffer[..total];
    }

    private static bool MatchesAny(byte[] header, byte[][] signatures) =>
        signatures.Any(sig => header.Length >= sig.Length && header.Take(sig.Length).SequenceEqual(sig));
}
