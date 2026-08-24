using HRMS.Application.Interfaces.Biometric;
using HRMS.Infrastructure.Biometric;

namespace HRMS.API.Extensions;

/// <summary>
/// DI registration for biometric capabilities (FIX-1).
/// Call services.AddBiometricCapabilities() from ServiceExtensions.AddInfrastructure
/// or from Program.cs.
///
/// Registration:
///   - IBiometricCapabilityService → BiometricCapabilityService (singleton — no state changes)
///
/// BiometricCapabilityService drives two behaviours at runtime:
///   1. GET /api/biometric/capabilities — surfaces which providers are implemented vs. stub
///   2. BiometricHostedService — skips stub providers during the auto-sync polling loop
/// </summary>
public static class BiometricServiceExtensions
{
    public static IServiceCollection AddBiometricCapabilities(this IServiceCollection services)
    {
        // Singleton: the capability registry is immutable at runtime.
        // Providers cannot become "implemented" without a code change + redeploy.
        services.AddSingleton<IBiometricCapabilityService, BiometricCapabilityService>();
        services.AddScoped<IBiometricSyncService, BiometricSyncService>();
        return services;
    }
}
