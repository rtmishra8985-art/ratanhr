namespace HRMS.Infrastructure.Data;

/// <summary>
/// Configuration model for primary + optional read-replica database connections.
///
/// Phase 2b: PostgreSQL WAL streaming replication design removed.
/// MySQL replication must be configured at the infrastructure level
/// (MySQL Group Replication or standard async replication).
/// See Documentation/MySqlMigrationGuide.md for details.
///
/// ── Application-level setup ─────────────────────────────────────────────────────────
///
/// Set in appsettings.json / environment variables:
///
///   "Database": {
///     "PrimaryConnection": "Server=primary-db;Port=3306;Database=hrms_db;User ID=hrms;Password=...;AllowPublicKeyRetrieval=True;SslMode=Required",
///     "ReplicaConnection": "Server=replica-db;Port=3306;Database=hrms_db;User ID=hrms;Password=...;AllowPublicKeyRetrieval=True;SslMode=Required",
///     "EnableReadReplica": true
///   }
///
/// When EnableReadReplica=false (or ReplicaConnection is absent), all traffic goes to
/// the primary and the application works as normal — no code changes required.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Primary (read-write) connection string.
    /// When set, this takes precedence over the legacy "DefaultConnection" key.
    /// </summary>
    public string? PrimaryConnection { get; set; }

    /// <summary>
    /// Read-replica (read-only) connection string.
    /// Routed to only when <see cref="EnableReadReplica"/> is true.
    /// Must point to a MySQL replica configured for Group Replication or async replication.
    /// </summary>
    public string? ReplicaConnection { get; set; }

    /// <summary>
    /// When true and <see cref="ReplicaConnection"/> is configured, read-only
    /// query contexts (<see cref="ReadReplicaDbContext"/>) are routed to the replica.
    /// Writes always go to the primary.
    /// Set to false to route everything to the primary (safe default).
    /// </summary>
    public bool EnableReadReplica { get; set; } = false;
}
