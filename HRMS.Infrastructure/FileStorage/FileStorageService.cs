using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HRMS.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<string?> SaveAsync(IFormFile? file, string subfolder);

    /// <summary>
    /// Item 9 — save with an explicit <see cref="UploadProfile"/>. Callers that know
    /// their accept rules (profile photo, resume, employee document) pass the profile
    /// so validation cannot be widened by an unexpected subfolder name.
    /// </summary>
    Task<string?> SaveAsync(IFormFile? file, string subfolder, UploadProfile? profile);

    Task<string?> SaveFileAsync(IFormFile? file, string subfolder);

    /// <summary>Alias of <see cref="SaveAsync(IFormFile?, string, UploadProfile?)"/>.</summary>
    Task<string?> SaveFileAsync(IFormFile? file, string subfolder, UploadProfile? profile);
    Task<Stream> RetrieveAsync(string relativePath);
    void Delete(string? relativePath);
}

/// <summary>
/// Persists uploaded files. All validation is delegated to the shared
/// <see cref="UploadValidator"/> (audit item 9) so that every IFormFile path in the
/// application enforces exactly the same five gates:
///   1. Presence / non-empty
///   2. Size — min(profile ceiling, FileUpload:MaxFileSizeMB)
///   3. Extension allow-list — per-endpoint profile, intersected with FileUpload:AllowedExtensions
///   4. Declared MIME must agree with the extension
///   5. Magic-byte signature of the real bytes must match the extension
/// The stored filename is always a server-generated GUID; the client-supplied
/// name never reaches the filesystem.
///
/// The per-endpoint profile is resolved from <paramref name="subfolder"/> via
/// <see cref="UploadProfile.ForSubfolder"/>, or supplied explicitly by callers
/// that know their accept rules (Recruitment resumes, employee documents).
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly string _uploadsRoot;
    private readonly FileUploadOptions _options;

    public FileStorageService(string uploadsRoot, IOptions<FileUploadOptions>? options = null)
    {
        _uploadsRoot = uploadsRoot;
        _options = options?.Value ?? new FileUploadOptions
        {
            MaxFileSizeMB = 10,
            AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx", ".xls", ".xlsx" }
        };
    }

    public Task<string?> SaveAsync(IFormFile? file, string subfolder)
        => SaveAsync(file, subfolder, null);

    public async Task<string?> SaveAsync(IFormFile? file, string subfolder, UploadProfile? profile)
    {
        // An absent optional upload is not an error — the caller stores null.
        if (file == null || file.Length == 0) return null;

        var effectiveProfile = profile ?? UploadProfile.ForSubfolder(subfolder);

        var result = UploadValidator.Validate(
            file,
            effectiveProfile,
            _options.MaxFileSizeMB,
            _options.AllowedExtensions);

        if (!result.IsValid)
            throw new FileUploadValidationException(result.Error!);

        // Save under the server-generated, GUID-based safe name.
        var dir = Path.Combine(_uploadsRoot, subfolder);
        Directory.CreateDirectory(dir);
        var filename = result.SafeFileName!;
        var fullPath = Path.Combine(dir, filename);
        using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{subfolder}/{filename}";
    }

    // Backward-compat aliases used in EmployeeService, CompanyService etc.
    public async Task<string?> SaveFileAsync(IFormFile? file, string subfolder)
        => await SaveAsync(file, subfolder, null);

    public async Task<string?> SaveFileAsync(IFormFile? file, string subfolder, UploadProfile? profile)
        => await SaveAsync(file, subfolder, profile);


    public Task<Stream> RetrieveAsync(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new FileNotFoundException("The stored document path is empty.");

        var uploadsRootFull = Path.GetFullPath(_uploadsRoot);
        var sanitized = relativePath.TrimStart('/');
        if (sanitized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring("uploads/".Length);

        var fullPath = Path.GetFullPath(Path.Combine(uploadsRootFull, sanitized));
        if (!fullPath.StartsWith(uploadsRootFull + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The stored document path is invalid.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The stored document could not be found.", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;

        // FIX 5: The previous Path.Combine(_uploadsRoot, "..", ...) allowed a crafted
        // relativePath like "../../etc/passwd" to resolve outside the uploads directory
        // and delete arbitrary files. We now resolve to a canonical path and reject any
        // result that does not start within _uploadsRoot.
        var uploadsRootFull = Path.GetFullPath(_uploadsRoot);

        // Stored paths are "/uploads/<subfolder>/<file>"; strip the leading "/uploads/"
        // prefix and resolve relative to the uploads root.
        var sanitized = relativePath.TrimStart('/');
        if (sanitized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized.Substring("uploads/".Length);

        var fullPath = Path.GetFullPath(Path.Combine(uploadsRootFull, sanitized));

        // Guard: the resolved path must be inside _uploadsRoot
        if (!fullPath.StartsWith(uploadsRootFull + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            // Path traversal attempt — silently ignore (do not leak information via exception)
            return;
        }

        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    public void DeleteFile(string? relativePath) => Delete(relativePath);
}

public class FileUploadValidationException : Exception
{
    public FileUploadValidationException(string message) : base(message) { }
}
