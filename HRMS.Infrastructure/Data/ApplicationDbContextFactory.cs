using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRMS.Infrastructure.Data;

/// <summary>
/// IDesignTimeDbContextFactory for ApplicationDbContext.
/// Used exclusively by EF Core tooling (dotnet-ef migrations/database update) at design time.
/// Reads only ConnectionStrings__DefaultConnection from the environment — no JWT, no
/// encryption key, no Redis — so the tool can discover the context without fully starting
/// the application.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // EF tooling sets this env var; docker run --env-file also injects it.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Environment variable ConnectionStrings__DefaultConnection is required for " +
                "design-time context creation. Set it before running dotnet-ef.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            // Pinned to the staging MySQL version by default. Set
            // MIGRATIONS_SERVER_VERSION (e.g. "8.0.45-mysql") to target another
            // server when validating the chain against a different engine build.
            ServerVersion.Parse(
                Environment.GetEnvironmentVariable("MIGRATIONS_SERVER_VERSION")
                ?? "8.4.11-mysql"),
            mySqlOptions => mySqlOptions
                .MigrationsAssembly("HRMS.Infrastructure")
                .EnableRetryOnFailure(3)
        );

        // Pass null for IConfiguration and ITenantContext — design-time context
        // must not depend on runtime services.
        return new ApplicationDbContext(optionsBuilder.Options, config: null, tenant: null);
    }
}
