// Phase 8: Updated DbInitSql_Enables_Required_Extensions test.
// PostgreSQL extension assertions (uuid-ossp, pg_trgm) removed — MySQL does not use extensions.
// New assertion: db-init.sql must contain utf8mb4 charset and CREATE DATABASE.
// Fixed: DF1, DF2, DF4 — Dockerfile regression tests so bad digests cannot be re-introduced.
using System.Text.RegularExpressions;
using Xunit;

namespace HRMS.Tests;

public class DockerfileValidationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Dockerfile")))
            dir = dir.Parent;
        Assert.True(dir != null, "Repo root (directory containing Dockerfile) not found.");
        return dir!.FullName;
    }

    private static string ReadDockerfile()
    {
        // Walk up from the test assembly to find the repo root Dockerfile
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Dockerfile")))
            dir = dir.Parent;
        var path = dir != null ? Path.Combine(dir.FullName, "Dockerfile") : null;
        Assert.True(File.Exists(path), "Dockerfile not found in any ancestor directory of test output.");
        return File.ReadAllText(path!);
    }

    /// <summary>
    /// Fixed: DF1 — Fake sequential-pattern SHA256 digests (aaaabbbb… style) must not be present.
    /// </summary>
    [Fact]
    public void Dockerfile_Does_Not_Contain_Fake_Sequential_Digests()
    {
        var content = ReadDockerfile();
        // Fake digests look like: sha256:1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b
        // Pattern: 8 chars that cycle through 0-9a-f in a sequential/repeating stride ≥ 4
        var fakePattern = new Regex(@"sha256:[0-9a-f]{4}([0-9a-f]{4})\1{6}", RegexOptions.IgnoreCase);
        Assert.False(fakePattern.IsMatch(content),
            "Dockerfile contains a fake sequential SHA256 digest. Replace with real pinned digests.");
    }

    /// <summary>
    /// Fixed: DF2 — Must use 'database update', not the invalid 'database migrate' subcommand.
    /// </summary>
    [Fact]
    public void Dockerfile_Uses_Database_Update_Not_Database_Migrate()
    {
        var content = ReadDockerfile();
        Assert.DoesNotContain("dotnet ef database migrate", content, StringComparison.OrdinalIgnoreCase);

        // The migration image uses the pinned local tool manifest so the container
        // never performs a network-dependent global tool install. The command itself
        // lives in the migrate entrypoint script invoked by the Dockerfile.
        var entrypointPath = Path.Combine(RepoRoot(), "docker", "migrate-entrypoint.sh");
        var migrationCommandSource = content;
        if (File.Exists(entrypointPath))
        {
            Assert.DoesNotContain("dotnet ef database migrate", File.ReadAllText(entrypointPath), StringComparison.OrdinalIgnoreCase);
            migrationCommandSource += "\n" + File.ReadAllText(entrypointPath);
        }

        Assert.Contains("dotnet tool run dotnet-ef database update", migrationCommandSource, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fixed: DF4 — TreatWarningsAsErrors=false must not be present; warnings must be errors.
    /// </summary>
    [Fact]
    public void Dockerfile_Does_Not_Disable_TreatWarningsAsErrors()
    {
        var content = ReadDockerfile();
        Assert.DoesNotContain("TreatWarningsAsErrors=false", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Phase 8 / DF3 — db-init.sql must exist and be a MySQL 8.4 init script.
    /// PostgreSQL extension assertions (uuid-ossp, pg_trgm) removed — MySQL uses charsets not extensions.
    /// Asserts utf8mb4 character set and CREATE DATABASE are present.
    /// </summary>
    [Fact]
    public void DbInitSql_Is_MySQL_Compatible()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        var path = dir != null ? Path.Combine(dir.FullName, "scripts", "db-init.sql") : null;
        Assert.True(File.Exists(path), "scripts/db-init.sql not found.");

        var content = File.ReadAllText(path!);
        Assert.Contains("utf8mb4", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE DATABASE", content, StringComparison.OrdinalIgnoreCase);
        // Must NOT contain PostgreSQL-specific syntax
        Assert.DoesNotContain("pg_create_physical_replication_slot", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE EXTENSION", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Dockerfile must not reference PostgreSQL images or packages after MySQL migration.
    /// </summary>
    [Fact]
    public void Dockerfile_Does_Not_Reference_PostgreSQL()
    {
        var content = ReadDockerfile();
        Assert.DoesNotContain("npgsql", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pg_isready", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POSTGRES_", content, StringComparison.OrdinalIgnoreCase);
    }
}
