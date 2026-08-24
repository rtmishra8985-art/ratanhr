// FIX MED: Added System.ComponentModel.DataAnnotations attributes for IOptions validation.
// ValidateDataAnnotations() + ValidateOnStart() in ServiceExtensions will throw on startup
// if MaxFileSizeMB is outside the accepted range, preventing silent misconfiguration.
using System.ComponentModel.DataAnnotations;

namespace HRMS.Infrastructure.Security;

public class FileUploadOptions
{
    /// <summary>Maximum file upload size in megabytes. Must be between 1 and 100.</summary>
    [Range(1, 100, ErrorMessage = "FileUpload:MaxFileSizeMB must be between 1 and 100.")]
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Allowed file extensions (e.g. ".jpg", ".png", ".pdf").
    /// At least one entry is required when validation is active.
    /// </summary>
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}

// Audit item 9 — the legacy `MimeValidator` helper (declared-Content-Type
// signature check, "unknown MIME → allow through") was removed. It had no
// remaining callers: every upload path now goes through
// HRMS.Infrastructure.Security.UploadValidator, which fails closed and also
// enforces the extension allow-list, extension/MIME agreement and size ceiling.
