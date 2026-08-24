namespace HRMS.Application.Interfaces.Biometric;

/// <summary>
/// Reports which biometric providers are fully implemented versus which are stubs
/// awaiting hardware SDK integration. Used by the capabilities endpoint so the UI
/// can surface accurate status to operators instead of silently returning empty data.
/// (FIX-1: Biometric feature gap resolution)
/// </summary>
public interface IBiometricCapabilityService
{
    /// <summary>
    /// Returns capability information for every registered biometric provider.
    /// </summary>
    IReadOnlyList<BiometricProviderCapability> GetAllCapabilities();

    /// <summary>
    /// Returns capability information for a single provider by vendor name.
    /// Returns null if the vendor is not registered.
    /// </summary>
    BiometricProviderCapability? GetCapability(string vendorName);

    /// <summary>
    /// Returns the list of vendors that are fully implemented (not stubs).
    /// Only these vendors will be polled by BiometricHostedService.
    /// </summary>
    IReadOnlyList<string> GetImplementedVendors();
}

/// <summary>
/// Capability descriptor for a single biometric hardware provider.
/// </summary>
public sealed record BiometricProviderCapability(
    /// <summary>Registered vendor name (e.g. "ZKTeco", "eSSL").</summary>
    string VendorName,

    /// <summary>
    /// True = production-ready TCP/HTTP implementation is present.
    /// False = stub that returns empty data; awaiting vendor SDK integration.
    /// </summary>
    bool IsImplemented,

    /// <summary>Human-readable description of the integration status.</summary>
    string StatusDescription,

    /// <summary>
    /// For stubs: the SDK or API that must be integrated to complete this provider.
    /// Null for fully implemented providers.
    /// </summary>
    string? PendingIntegration
);
