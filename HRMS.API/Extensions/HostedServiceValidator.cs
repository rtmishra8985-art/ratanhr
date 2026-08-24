namespace HRMS.API.Extensions;

using Serilog;

/// <summary>
/// FIX 4: Startup validator for hosted services — prevents accidental duplicate registrations.
/// The EmailQueueWorker must be registered exactly once, or email delivery will create race conditions.
/// This validator runs during app startup and throws if any hosted service is duplicated.
/// </summary>
public static class HostedServiceValidator
{
    /// <summary>
    /// Validates that critical hosted services are registered exactly once.
    /// Call this during startup before running the application.
    /// </summary>
    /// <param name="app">The WebApplication instance.</param>
    /// <param name="environment">The HostingEnvironment (used to determine if checks are enforced).</param>
    /// <exception cref="InvalidOperationException">Thrown if a hosted service is duplicated.</exception>
    public static void ValidateHostedServices(this WebApplication app, IHostEnvironment environment)
    {
        // Only enforce in non-development environments
        if (environment.IsDevelopment())
        {
            Log.Debug("[HostedServiceValidator] Skipping validation in Development mode.");
            return;
        }

        var hostedServices = app.Services.GetServices<IHostedService>();
        var hostedServicesList = hostedServices.ToList();

        // Check for EmailQueueWorker duplicates
        var emailWorkerCount = hostedServicesList
            .Where(h => h.GetType().Name == "EmailQueueWorker")
            .Count();

        if (emailWorkerCount != 1)
        {
            throw new InvalidOperationException(
                $"[HostedServiceValidator] Expected exactly 1 EmailQueueWorker hosted service, " +
                $"found {emailWorkerCount}. This causes email delivery race conditions. " +
                $"Ensure services are registered only in ServiceExtensions.AddInfrastructure(), " +
                $"NOT in Program.cs. See the NOTE comment in Program.cs.");
        }

        // Check for BiometricLogCleanupService duplicates
        var biometricCleanupCount = hostedServicesList
            .Where(h => h.GetType().Name == "BiometricLogCleanupService")
            .Count();

        if (biometricCleanupCount > 1)
        {
            throw new InvalidOperationException(
                $"[HostedServiceValidator] Expected at most 1 BiometricLogCleanupService, " +
                $"found {biometricCleanupCount}. Register all hosted services in " +
                $"ServiceExtensions.AddInfrastructure().");
        }

        Log.Information($"[HostedServiceValidator] Hosted services validated successfully. " +
                       $"EmailQueueWorker={emailWorkerCount}, BiometricCleanup={biometricCleanupCount}");
    }
}
