namespace HRMS.Application.DTOs.Appreciation;

/// <summary>Response DTO for an appreciation record — avoids exposing the domain entity directly.</summary>
public class AppreciationDto
{
    public int      Id         { get; set; }
    public string   EmployeeId { get; set; } = string.Empty;
    public string?  Message    { get; set; }
    public string?  FilePath   { get; set; }
    public int?     CreatedBy  { get; set; }
    public DateTime CreatedAt  { get; set; }
}

/// <summary>
/// FIX 6: Typed form-binding DTO for the appreciation upload endpoint.
/// Adds a FluentValidation surface for EmployeeId, Message, and file metadata.
/// The controller binds the raw IFormFile separately (ASP.NET Core does not model-bind
/// IFormFile inside a custom class) and populates <see cref="FileSize"/> /
/// <see cref="FileExtension"/> before validation runs.
/// </summary>
public class UploadAppreciationDto
{
    /// <summary>Target employee's ID string.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Optional free-text appreciation message (max 2000 chars).</summary>
    public string? Message { get; set; }

    // Populated by the controller from IFormFile before calling the validator.
    /// <summary>File size in bytes — populated from IFormFile.Length.</summary>
    public long? FileSize { get; set; }

    /// <summary>File extension (e.g. ".pdf") — populated from IFormFile.FileName.</summary>
    public string? FileExtension { get; set; }
}
