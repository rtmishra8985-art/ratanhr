// FIX MED-03: Antivirus scanning interface.
// All file-upload paths inject this service and call ScanAsync() before
// persisting the file.  The ClamAV implementation is in
// HRMS.Infrastructure/Services/ClamAvVirusScanService.cs.
namespace HRMS.Application.Interfaces;

/// <summary>Abstracts AV scanning so the application layer stays infra-agnostic.</summary>
public interface IVirusScanService
{
    /// <summary>
    /// Scan <paramref name="fileStream"/> for malware.
    /// </summary>
    /// <param name="fileStream">Seekable stream of the uploaded bytes.</param>
    /// <param name="fileName">Original filename (used only for log messages).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="VirusScanResult"/> where <c>IsClean = true</c> means no threat was found.
    /// </returns>
    Task<VirusScanResult> ScanAsync(Stream fileStream, string fileName,
        CancellationToken ct = default);
}

/// <summary>Result returned by <see cref="IVirusScanService.ScanAsync"/>.</summary>
/// <param name="IsClean">True when the file is free of known threats.</param>
/// <param name="ThreatName">Name of the detected threat, or <c>null</c> when clean.</param>
public sealed record VirusScanResult(bool IsClean, string? ThreatName);
