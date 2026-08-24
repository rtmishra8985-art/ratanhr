using HRMS.Domain.Common;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Attendance;

/// <summary>
/// Represents a registered biometric hardware device (reader/terminal) in the HRMS.
/// Each device belongs to one company (tenant) and uses exactly one vendor provider.
/// </summary>
public class BiometricDevice : ICompanyOwned
{
    public int    Id        { get; set; }
    public int?   CompanyId { get; set; }

    /// <summary>Friendly display name for the device (e.g. "Main Gate Reader").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Vendor/provider type — must match a registered IBiometricProvider.VendorName.</summary>
    public BiometricProviderType ProviderType { get; set; }

    /// <summary>
    /// Vendor name string stored for query/display — kept in sync with ProviderType.
    /// e.g. "ZKTeco", "eSSL", "Matrix".
    /// </summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>IP address or hostname of the device on the LAN.</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>TCP port the device listens on (default: 4370 for ZKTeco).</summary>
    public int Port { get; set; } = 4370;

    /// <summary>Optional serial number printed on the device label.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Physical location description (e.g. "Ground Floor — Entrance").</summary>
    public string? Location { get; set; }

    /// <summary>Current operational status.</summary>
    public BiometricStatus Status { get; set; } = BiometricStatus.Active;

    /// <summary>Whether the device polling is enabled. False = device is administratively excluded from sync.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>UTC timestamp of the last successful sync from this device.</summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>UTC timestamp of the last successful connectivity ping.</summary>
    public DateTime? LastPingAt { get; set; }

    /// <summary>Last error message returned by the provider, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Firmware or SDK version string reported by the device.</summary>
    public string? FirmwareVersion { get; set; }

    /// <summary>Number of enrolled users reported by the device (updated after each ping).</summary>
    public int? EnrolledUserCount { get; set; }

    /// <summary>Optional JSON bag for vendor-specific connection parameters.</summary>
    public string? ConnectionParams { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────────────────────
    public ICollection<BiometricLog>         Logs         { get; set; } = [];
    public ICollection<BiometricSyncHistory> SyncHistories { get; set; } = [];
}
