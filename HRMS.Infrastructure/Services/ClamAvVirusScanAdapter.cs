using HRMS.Application.Interfaces;

namespace HRMS.Infrastructure.Services;

/// <summary>
/// Adapts the infrastructure-level ClamAV service to the application-level
/// abstraction consumed by the global antivirus action filter.
/// </summary>
public sealed class ClamAvVirusScanAdapter : IVirusScanService
{
    private readonly IClamAvVirusScanService _inner;

    public ClamAvVirusScanAdapter(IClamAvVirusScanService inner) => _inner = inner;

    public async Task<VirusScanResult> ScanAsync(
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var result = await _inner.ScanAsync(fileStream, fileName, ct);
        return new VirusScanResult(result.IsClean, result.Threat);
    }
}