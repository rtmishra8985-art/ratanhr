namespace HRMS.Application.Interfaces.Biometric;

/// <summary>
/// Resolves the correct <see cref="IBiometricProvider"/> implementation for
/// a given vendor name string (e.g. "ZKTeco", "eSSL").
/// Register all providers via DI, then inject this factory wherever
/// biometric data needs to be pulled from hardware.
/// </summary>
public interface IBiometricProviderFactory
{
    /// <summary>
    /// Returns the registered provider for <paramref name="vendorName"/>.
    /// Throws <see cref="NotSupportedException"/> for unknown vendors.
    /// </summary>
    IBiometricProvider GetProvider(string vendorName);

    /// <summary>Returns the names of all registered vendors.</summary>
    IReadOnlyList<string> RegisteredVendors { get; }
}
