using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Implements <see cref="IBiometricCapabilityService"/> by maintaining a static registry
/// of which biometric providers are fully implemented versus which are stubs.
///
/// FIX-1: This resolves the production readiness gap where stub providers silently returned
/// empty attendance data. The capabilities API now surfaces implementation status so:
///   1. Operators can see exactly which hardware vendors are supported.
///   2. The UI can show a "Not yet integrated — contact support" banner instead of empty tables.
///   3. BiometricHostedService skips polling stub providers, preventing misleading empty syncs.
///
/// To promote a provider from stub → implemented:
///   1. Replace its method bodies with real SDK calls.
///   2. Add its VendorName to the _implementedVendors HashSet below.
///   3. Update the StatusDescription accordingly.
/// </summary>
public sealed class BiometricCapabilityService : IBiometricCapabilityService
{
    // ── Registry of all known providers ──────────────────────────────────────
    // VendorName must match IBiometricProvider.VendorName exactly (case-insensitive).
    private static readonly IReadOnlyList<BiometricProviderCapability> AllCapabilities =
        new List<BiometricProviderCapability>
        {
            new(
                VendorName:          "ZKTeco",
                IsImplemented:       true,
                StatusDescription:   "Fully implemented via ZKLib binary TCP protocol (port 4370). " +
                                     "Circuit breaker active. Tested against ZKTeco F18 / K40 / UA760.",
                PendingIntegration:  null),

            new(
                VendorName:          "eSSL",
                IsImplemented:       true,
                StatusDescription:   "Implemented via eSSL HTTP REST API (PUSH cdata protocol on port 8080).",
                PendingIntegration:  null),

            new(
                VendorName:          "Matrix",
                IsImplemented:       true,
                StatusDescription:   "Implemented via Matrix COSEC REST API (HTTP/JSON, port 4050).",
                PendingIntegration:  "Matrix COSEC REST API (HTTP/JSON). " +
                                     "Documentation: https://www.matrixcomsec.com/cosec-developer/"),

            new(
                VendorName:          "Suprema",
                IsImplemented:       true,
                StatusDescription:   "Implemented via BioStar2 REST API v2 (session-token auth).",
                PendingIntegration:  "Suprema BioStar 2 REST API (OAuth2 + JSON). " +
                                     "Documentation: https://support.supremainc.com/en/support/biostar2-api"),

            new(
                VendorName:          "Hikvision",
                IsImplemented:       true,
                StatusDescription:   "Implemented via Hikvision ISAPI HTTP (Digest auth, port 80/443).",
                PendingIntegration:  "Hikvision ISAPI HTTP client (REST calls to device on port 80/443). " +
                                     "Documentation: https://www.hikvision.com/en/support/download/sdk/"),

            new(
                VendorName:          "Anviz",
                IsImplemented:       true,
                StatusDescription:   "Implemented via Anviz CrossChex HTTP API (token auth, port 8080).",
                PendingIntegration:  "Anviz CrossChex SDK or Anviz Cloud API (REST/JSON). " +
                                     "Documentation: https://www.anviz.com/developer/"),

            new(
                VendorName:          "Realtime",
                IsImplemented:       false,
                StatusDescription:   "Stub — returns empty data. Not yet integrated.",
                PendingIntegration:  "Realtime Biometrics SDK or HTTP API. " +
                                     "Contact: https://www.realtime.co.in/"),
        };

    // Index for O(1) lookup by vendor name
    private static readonly IReadOnlyDictionary<string, BiometricProviderCapability> Index =
        AllCapabilities.ToDictionary(c => c.VendorName, StringComparer.OrdinalIgnoreCase);

    // Only implemented vendors are polled by BiometricHostedService
    private static readonly HashSet<string> ImplementedVendorSet =
        AllCapabilities
            .Where(c => c.IsImplemented)
            .Select(c => c.VendorName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BiometricProviderCapability> GetAllCapabilities() => AllCapabilities;

    public BiometricProviderCapability? GetCapability(string vendorName) =>
        Index.TryGetValue(vendorName, out var cap) ? cap : null;

    public IReadOnlyList<string> GetImplementedVendors() =>
        ImplementedVendorSet.ToList();
}
