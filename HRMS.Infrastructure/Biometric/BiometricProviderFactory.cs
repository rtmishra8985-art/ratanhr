using HRMS.Application.Interfaces.Biometric;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// Default factory that resolves <see cref="IBiometricProvider"/> implementations
/// by vendor name. All registered providers are injected via DI.
/// </summary>
public sealed class BiometricProviderFactory : IBiometricProviderFactory
{
    private readonly IReadOnlyDictionary<string, IBiometricProvider> _providers;

    public BiometricProviderFactory(IEnumerable<IBiometricProvider> providers)
    {
        _providers = providers.ToDictionary(
            p => p.VendorName,
            p => p,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> RegisteredVendors =>
        _providers.Keys.OrderBy(k => k).ToList();

    public IBiometricProvider GetProvider(string vendorName)
    {
        if (_providers.TryGetValue(vendorName, out var provider))
            return provider;

        throw new NotSupportedException(
            $"Biometric vendor '{vendorName}' is not registered. " +
            $"Registered vendors: {string.Join(", ", RegisteredVendors)}");
    }
}
