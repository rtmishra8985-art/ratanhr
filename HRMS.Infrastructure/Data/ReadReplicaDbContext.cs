using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Data;

/// <summary>
/// A read-only DbContext that routes non-mutating queries to a MySQL replica when configured.
///
/// Use this context for reports, dashboards, and read-heavy lookups.
/// All write operations (INSERT / UPDATE / DELETE) must still use
/// <see cref="ApplicationDbContext"/> which points to the primary server.
///
/// Phase 2b: PostgreSQL WAL streaming replica design removed.
/// MySQL read replica requires MySQL Group Replication or standard async replication
/// configured at the infrastructure level — WAL streaming does not apply to MySQL.
/// DEFERRED (intentional, Phase 2 decision — not a defect):
/// Physical MySQL replication (Group Replication or classic async replication) is an
/// infrastructure/deployment concern and is out of scope for the application codebase.
/// The application side is already complete: ReadReplicaDbContext is registered in
/// ServiceExtensions.AddInfrastructure() and is bound to Database:ReplicaConnection
/// when Database:EnableReadReplica=true; otherwise it transparently uses the primary.
/// No code change is required to enable it — set the two settings once a replica host
/// exists. Tracking: HRMS-INFRA-READ-REPLICA (owner: platform/infra, target: post-Phase 3).
///
/// When <see cref="DatabaseOptions.EnableReadReplica"/> is false or
/// <see cref="DatabaseOptions.ReplicaConnection"/> is absent, this context
/// automatically falls back to the primary connection — the application continues
/// working without any code changes.
///
/// Registration is handled in ServiceExtensions.AddInfrastructure().
/// </summary>
public class ReadReplicaDbContext : ApplicationDbContext
{
    public ReadReplicaDbContext(
        DbContextOptions<ReadReplicaDbContext> options,
        IConfiguration? config = null)
        : base(CreateBaseOptions(options), config)
    { }

    /// <summary>
    /// EF Core requires the options to be of type <see cref="DbContextOptions{TContext}"/>
    /// where TContext matches the concrete class. This helper promotes the typed options
    /// to the base DbContextOptions so the ApplicationDbContext constructor accepts them.
    /// </summary>
    private static DbContextOptions<ApplicationDbContext> CreateBaseOptions(
        DbContextOptions<ReadReplicaDbContext> options)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        foreach (var extension in options.Extensions)
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder.Options;
    }
}
