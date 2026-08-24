// H-03 FIX: Integration tests that verify AES-256-GCM PII encryption is actually
// exercised in the database context.
//
// Strategy (two-context approach — SQLite + raw ADO.NET read-back):
//
//   Context A  (encrypted): saves the entity via a normal SQLite-backed EF Core
//   context that has the encryption key configured.  EF Core's write converter
//   (enc.Encrypt) runs before the value reaches the SQLite column, so the column
//   stores the "enc:v1:…" ciphertext.
//
//   Raw ADO.NET read: we read the column value back directly through a plain
//   SqliteCommand on the same open connection — no EF Core materialization,
//   no value converter, no model cache — so we see exactly what is stored
//   on disk/in-memory.
//
// Why ADO.NET instead of a second EF Core context?
//   EF Core's default IModelCacheKeyFactory keys compiled models by DbContext
//   type.  Both the encrypted and plain contexts are ApplicationDbContext, so EF
//   Core returns the same cached model (built with converters) for both.  A
//   "no-converter" context therefore still decrypts on read, hiding the
//   at-rest ciphertext.  An ADO.NET SqliteCommand has no model layer at all —
//   it returns the raw text stored in the column.
//
// Why IgnoreQueryFilters() in CreateInMemoryDb_NullConfig_DoesNotEncryptPii?
//   The Employee HasQueryFilter closes over _tenant (null in test contexts).
//   EF Core's in-process expression evaluator dereferences that null before the
//   C# || short-circuit fires, producing NullReferenceException inside the
//   compiled query lambda.  IgnoreQueryFilters() prevents that expression from
//   being compiled or executed.
using HRMS.Domain.Entities.Employee;
using HRMS.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Tests.Security;

/// <summary>
/// H-03 FIX: Verifies that PII fields persisted via ApplicationDbContext are
/// actually encrypted at rest (SQLite TEXT column holds "enc:v1:…"), and that
/// the full round-trip decrypt returns the original plaintext.
/// </summary>
public class PiiEncryptionIntegrationTests
{
    // ── Helper ─────────────────────────────────────────────────────────────
    private static HRMS.Infrastructure.Security.AesEncryptionService MakeEncSvc()
        => new(TestHelpers.TestEncryptionKey);

    /// <summary>
    /// Reads a single column from the employees table via raw ADO.NET.
    /// Returns exactly what is stored in SQLite — no EF Core converter runs.
    /// </summary>
    private static async Task<string?> ReadRawColumnAsync(
        SqliteConnection conn, int employeeId, string columnName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {columnName} FROM employees WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", employeeId);
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? null : (string)result;
    }

    // ── Aadhaar number encryption ───────────────────────────────────────────

    [Fact]
    public async Task Aadhaar_IsEncryptedAtRest_WhenSavedViaDbContext()
    {
        // Arrange: save via an encrypted SQLite context.
        var (db, conn) = TestHelpers.CreateSqliteDbWithEncryption();
        var enc = MakeEncSvc();
        try
        {
            var emp = new Employee
            {
                EmployeeCode = "EMP-PII-001",
                FullName     = "Test Employee",
                CompanyId    = 1,
                Email        = "pii@example.com",
                IsActive     = true,
                Aadhaar      = "123456789012",   // plain text going IN
            };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            int savedId = emp.Id;
            db.Dispose();

            // Act: read the raw column value via ADO.NET — no EF Core layer.
            string? rawAadhaar = await ReadRawColumnAsync(conn, savedId, "aadhaar");

            // Assert: at-rest value is the ciphertext, not the original plaintext.
            Assert.NotNull(rawAadhaar);
            Assert.StartsWith("enc:v1:", rawAadhaar!);
            // Full round-trip: the service decrypts back to the original value.
            Assert.Equal("123456789012", enc.Decrypt(rawAadhaar));
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task Pan_IsEncryptedAtRest_WhenSavedViaDbContext()
    {
        var (db, conn) = TestHelpers.CreateSqliteDbWithEncryption();
        var enc = MakeEncSvc();
        try
        {
            var emp = new Employee
            {
                EmployeeCode = "EMP-PII-002",
                FullName     = "Test PAN",
                CompanyId    = 1,
                Email        = "pan@example.com",
                IsActive     = true,
                PAN          = "ABCDE1234F",
            };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            int savedId = emp.Id;
            db.Dispose();

            string? rawPan = await ReadRawColumnAsync(conn, savedId, "pan");

            Assert.NotNull(rawPan);
            Assert.StartsWith("enc:v1:", rawPan!);
            Assert.Equal("ABCDE1234F", enc.Decrypt(rawPan));
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task BankAccount_IsEncryptedAtRest_WhenSavedViaDbContext()
    {
        var (db, conn) = TestHelpers.CreateSqliteDbWithEncryption();
        var enc = MakeEncSvc();
        try
        {
            var emp = new Employee
            {
                EmployeeCode  = "EMP-PII-003",
                FullName      = "Test Bank",
                CompanyId     = 1,
                Email         = "bank@example.com",
                IsActive      = true,
                AccountNumber = "9876543210001234",
            };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            int savedId = emp.Id;
            db.Dispose();

            string? rawAccount = await ReadRawColumnAsync(conn, savedId, "account_number");

            Assert.NotNull(rawAccount);
            Assert.StartsWith("enc:v1:", rawAccount!);
            Assert.Equal("9876543210001234", enc.Decrypt(rawAccount));
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task NullPii_IsStoredAsNull_NotEncryptedEmptyString()
    {
        var (db, conn) = TestHelpers.CreateSqliteDbWithEncryption();
        try
        {
            var emp = new Employee
            {
                EmployeeCode = "EMP-PII-NULL",
                FullName     = "Null PII",
                CompanyId    = 1,
                Email        = "null@example.com",
                IsActive     = true,
                Aadhaar      = null,
                PAN          = null,
            };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            int savedId = emp.Id;
            db.Dispose();

            // NULL input must produce NULL at rest — never an encrypted empty string.
            string? rawAadhaar = await ReadRawColumnAsync(conn, savedId, "aadhaar");
            string? rawPan     = await ReadRawColumnAsync(conn, savedId, "pan");

            Assert.Null(rawAadhaar);
            Assert.Null(rawPan);
        }
        finally
        {
            conn.Dispose();
        }
    }

    // ── Context comparison: CreateInMemoryDb (null config) skips encryption ─

    [Fact]
    public void CreateInMemoryDb_NullConfig_DoesNotEncryptPii()
    {
        // This test documents the known limitation of CreateInMemoryDb (H-03 root
        // cause): encryption is skipped so PII is stored as plain text.  Tests that
        // need encryption must use CreateSqliteDbWithEncryption() instead.
        using var db = TestHelpers.CreateInMemoryDb();

        var emp = new Employee
        {
            EmployeeCode = "EMP-PLAIN",
            FullName     = "Plain Employee",
            CompanyId    = 1,
            Email        = "plain@example.com",
            IsActive     = true,
            Aadhaar      = "plain-aadhaar",
        };
        db.Employees.Add(emp);
        db.SaveChanges();

        // IgnoreQueryFilters() is required: the Employee HasQueryFilter closes over
        // _tenant (null in test contexts) and EF Core's expression evaluator would
        // dereference that null before the || short-circuit fires, causing
        // NullReferenceException inside the compiled query lambda.
        db.ChangeTracker.Clear();
        var persisted = db.Employees
            .IgnoreQueryFilters()
            .FirstOrDefault(e => e.Id == emp.Id);

        // With null config the value converters are inactive — value stored as-is.
        Assert.Equal("plain-aadhaar", persisted?.Aadhaar);
        Assert.False(persisted?.Aadhaar?.StartsWith("enc:v1:") ?? false,
            "CreateInMemoryDb with null config must NOT encrypt — use CreateSqliteDbWithEncryption for that.");
    }
}
