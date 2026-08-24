// Updated: added CreateSqliteDb() for query-count regression tests (Fix 7).
// SQLite in-process databases execute real SQL, allowing DbCommandInterceptor
// to count queries — unlike the InMemory provider which translates everything
// in-process and never issues SQL commands.
//
// Updated: added GenerateTestRsaKeyPair() so JWT tests can use RS256 (the
// production algorithm) without needing pre-generated PEM fixtures in source.
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace HRMS.Tests;

public static class TestHelpers
{
    // ── InMemory (fast, no SQL) ────────────────────────────────────────────
    // Use for unit tests that do not need to count SQL queries.
    public static ApplicationDbContext CreateInMemoryDb(ITenantContext? tenant = null)
        => CreateNamedInMemoryDb(Guid.NewGuid().ToString(), config: null, tenant: tenant);

    /// <summary>
    /// Creates an in-memory context that shares the same named database store as any
    /// other context created with the same <paramref name="dbName"/>.  Used by PII
    /// encryption tests to read back raw (unconverted) values from a second context
    /// that has no value converters.
    /// </summary>
    public static ApplicationDbContext CreateNamedInMemoryDb(
        string dbName, IConfiguration? config = null, ITenantContext? tenant = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            // Suppress the warning that EF Core in-memory provider raises when code
            // calls BeginTransactionAsync — BulkGeneratePayslipsAsync does this to
            // keep the batch atomic, but the in-memory provider ignores transactions.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options, config: config, tenant: tenant);
        db.Database.EnsureCreated();
        return db;
    }

    // ── SQLite in-process (real SQL, supports query counting) ─────────────
    // Use for N+1 regression tests that need QueryCounterInterceptor.
    // The caller owns the SqliteConnection and must dispose both after the test.
    public static (ApplicationDbContext db, SqliteConnection connection) CreateSqliteDb(
        QueryCounterInterceptor? interceptor = null,
        ITenantContext? tenant = null)
    {
        // Keep the connection open for the lifetime of the context so the in-memory
        // SQLite database is not destroyed when the context closes a connection.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection);

        if (interceptor != null)
            builder.AddInterceptors(interceptor);

        var db = new ApplicationDbContext(builder.Options, config: null, tenant: tenant);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    // ── SQLite with AES-256-GCM encryption (H-03 FIX: two-context at-rest) ──
    // EF Core's InMemory provider stores model values, not provider values, so
    // value converters are NOT applied at the storage layer.  This means the
    // two-context at-rest verification approach does not work with InMemory:
    // the second context always reads plaintext regardless of converters.
    //
    // SQLite in-memory DOES apply value converters: the write expression runs
    // before the value reaches the SQLite file (TEXT column), and the read
    // expression runs after.  A second context on the same open connection but
    // with no converter therefore reads back the raw encrypted string, which is
    // exactly what the at-rest tests need to assert.
    //
    // Usage pattern in tests:
    //   var (db, conn) = TestHelpers.CreateSqliteDbWithEncryption();
    //   // ... save via db, then dispose db ...
    //   using var rawDb = TestHelpers.OpenSqliteDbOnConnection(conn, config: null);
    //   var raw = rawDb.Employees.IgnoreQueryFilters()
    //                  .FirstOrDefault(e => e.Id == id);
    //   conn.Dispose();   // caller owns the connection lifetime

    /// <summary>
    /// Creates a SQLite in-memory database with AES-256-GCM PII encryption
    /// enabled.  Returns both the context and the open connection.
    /// <para>
    /// The caller MUST keep <paramref name="connection"/> open (and dispose it
    /// after all contexts on that database are done) because closing the
    /// SQLite in-memory connection destroys its data.
    /// </para>
    /// </summary>
    public static (ApplicationDbContext db, SqliteConnection connection)
        CreateSqliteDbWithEncryption(ITenantContext? tenant = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options,
            config: BuildEncryptionConfig(),
            tenant: tenant);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    /// <summary>
    /// Opens a second (or subsequent) context on an already-open SQLite
    /// connection.  Pass <c>config: null</c> to get a context without value
    /// converters so the raw (provider-level) bytes stored by the first
    /// context are returned unmodified.
    /// </summary>
    public static ApplicationDbContext OpenSqliteDbOnConnection(
        SqliteConnection connection,
        IConfiguration? config = null,
        ITenantContext? tenant = null)
        => new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options,
            config: config,
            tenant: tenant);

    // ── InMemory with AES-256-GCM encryption enabled (H-03 FIX) ─────────────
    // CreateInMemoryDb passes config:null which skips PII value converters so
    // encrypted fields (Aadhaar, PAN, bank account) are stored as plain text in tests.
    // This overload provides a real IConfiguration with a 32-byte AES-256 key so
    // the value converters encrypt/decrypt exactly as they do in production.
    //
    // Use this helper whenever a test asserts on encrypted PII storage or verifies
    // the AES-256-GCM round-trip.  The key is test-only — never reuse in production.
    public const string TestEncryptionKey = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI=";

    /// <summary>Builds the IConfiguration that enables PII value converters.</summary>
    public static IConfiguration BuildEncryptionConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:EncryptionKey"] = TestEncryptionKey
            })
            .Build();

    /// <summary>
    /// Creates an in-memory context with AES-256-GCM PII encryption enabled and returns
    /// both the context and the named-database identifier so a second, converter-free
    /// context can be opened on the same store to verify at-rest encryption.
    /// </summary>
    public static (ApplicationDbContext db, string dbName) CreateInMemoryDbWithEncryptionNamed(
        ITenantContext? tenant = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var db = CreateNamedInMemoryDb(dbName, config: BuildEncryptionConfig(), tenant: tenant);
        return (db, dbName);
    }

    /// <summary>Convenience overload — returns only the context (backward compat).</summary>
    public static ApplicationDbContext CreateInMemoryDbWithEncryption(ITenantContext? tenant = null)
        => CreateInMemoryDbWithEncryptionNamed(tenant).db;

    // ── RSA key pair generation for JWT tests ─────────────────────────────
    // Generates a fresh RSA-2048 key pair (PKCS#8 private + SPKI public) each call.
    // The returned PEM strings are accepted by JwtService.GenerateToken / ValidateToken
    // and EnvironmentValidator.Validate (which checks for "PRIVATE KEY" / "PUBLIC KEY").
    //
    // Call once per test class as a static readonly field to avoid the ~100 ms cost
    // of RSA key generation on every test method:
    //
    //   private static readonly (string Priv, string Pub) Keys =
    //       TestHelpers.GenerateTestRsaKeyPair();
    public static (string PrivatePem, string PublicPem) GenerateTestRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        // ExportPkcs8PrivateKeyPem → "-----BEGIN PRIVATE KEY-----" (PKCS#8)
        // ExportSubjectPublicKeyInfoPem → "-----BEGIN PUBLIC KEY-----" (SPKI)
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }
}
