// Updated: added tests for BcryptPasswordHasher (the configurable work-factor helper)
// in addition to the existing raw BCrypt.Net tests. These exercise the path that
// production code (AuthService, SeedAsync in Program.cs) actually uses.
using HRMS.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Tests for BCrypt password hashing used throughout the HRMS authentication flow.
/// Verifies: correct verification, wrong password rejection, hash uniqueness (salting),
/// plaintext-never-stored guarantee, and the configurable work-factor path via
/// <see cref="BcryptPasswordHasher"/> — the wrapper that production code calls.
/// </summary>
public class PasswordHashingTests
{
    // ── Raw BCrypt.Net correctness (algorithm-level guarantees) ─────────────

    [Fact]
    public void HashPassword_ThenVerify_ReturnsTrue()
    {
        const string password = "StrongP@ssw0rd!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash));
    }

    [Fact]
    public void HashPassword_WrongPassword_ReturnsFalse()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("correct-password");

        Assert.False(BCrypt.Net.BCrypt.Verify("wrong-password", hash));
    }

    [Fact]
    public void HashPassword_SameInput_ProducesDifferentHashes()
    {
        // BCrypt incorporates a random salt; identical inputs must not produce identical hashes.
        const string password = "SamePassword123!";
        var hash1 = BCrypt.Net.BCrypt.HashPassword(password);
        var hash2 = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
        // Both hashes must still verify correctly against the original password.
        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash1));
        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash2));
    }

    [Fact]
    public void HashPassword_OutputDoesNotContainPlaintext()
    {
        const string password = "S3cr3tP@ssword!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.DoesNotContain(password, hash);
    }

    [Fact]
    public void HashPassword_EmptyPassword_StillHashes()
    {
        // Edge case: empty passwords should be hashable and verifiable consistently.
        var hash = BCrypt.Net.BCrypt.HashPassword(string.Empty);

        Assert.True(BCrypt.Net.BCrypt.Verify(string.Empty, hash));
        Assert.False(BCrypt.Net.BCrypt.Verify("not-empty", hash));
    }

    [Fact]
    public void HashPassword_SpecialCharacters_RoundTripsCorrectly()
    {
        const string password = "P@$$w0rd!#%^&*()_+{}|:<>?";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash));
    }

    // ── BcryptPasswordHasher — configurable work factor (production path) ───
    // Production code calls BcryptPasswordHasher.Hash(password, config) rather than
    // BCrypt.Net.BCrypt.HashPassword() directly. These tests verify the wrapper reads
    // the configured work factor, applies it, and rejects out-of-range values.

    [Fact]
    public void BcryptPasswordHasher_DefaultWorkFactor_HashesAndVerifiesCorrectly()
    {
        // No explicit work factor → DefaultWorkFactor (12) is used.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var hash = BcryptPasswordHasher.Hash("TestPassword123!", config);

        Assert.True(BCrypt.Net.BCrypt.Verify("TestPassword123!", hash));
        // BCrypt hash format: $2a$WW$... where WW is the two-digit work factor.
        Assert.Contains($"${BcryptPasswordHasher.DefaultWorkFactor:D2}$", hash);
    }

    [Fact]
    public void BcryptPasswordHasher_ConfiguredWorkFactor4_IsEmbeddedInHash()
    {
        // Use work factor 4 (minimum) in tests to keep hashing fast.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BcryptPasswordHasher.ConfigurationKey] = "4"
            }).Build();

        var hash = BcryptPasswordHasher.Hash("TestPassword123!", config);

        // Work factor 4 must be reflected in the hash string.
        Assert.Contains("$04$", hash);
        // And the hash must still verify correctly.
        Assert.True(BCrypt.Net.BCrypt.Verify("TestPassword123!", hash));
    }

    [Fact]
    public void BcryptPasswordHasher_ExistingHashFromDifferentWorkFactor_StillVerifies()
    {
        // Users hashed with work factor 10 must still be verifiable after the
        // deployment upgrades to work factor 12. BCrypt embeds the cost in the hash,
        // so Verify() always uses the correct factor regardless of the current config.
        var hash10 = BCrypt.Net.BCrypt.HashPassword("SamePassword!", workFactor: 10);
        var hash12 = BCrypt.Net.BCrypt.HashPassword("SamePassword!", workFactor: 12);

        Assert.True(BCrypt.Net.BCrypt.Verify("SamePassword!", hash10));
        Assert.True(BCrypt.Net.BCrypt.Verify("SamePassword!", hash12));
    }

    [Fact]
    public void BcryptPasswordHasher_WorkFactorAboveMax_ThrowsInvalidOperationException()
    {
        // Work factor > 31 must be rejected at startup to prevent accidental
        // DoS via extremely slow hashing.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BcryptPasswordHasher.ConfigurationKey] = "32" // above max (31)
            }).Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => BcryptPasswordHasher.Hash("password", config));
        Assert.Contains(BcryptPasswordHasher.ConfigurationKey, ex.Message);
    }

    [Fact]
    public void BcryptPasswordHasher_WorkFactorBelowMin_ThrowsInvalidOperationException()
    {
        // Work factor < 4 must be rejected — values below 4 are too weak for production
        // and some BCrypt implementations don't support them reliably.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BcryptPasswordHasher.ConfigurationKey] = "3" // below min (4)
            }).Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => BcryptPasswordHasher.Hash("password", config));
        Assert.Contains(BcryptPasswordHasher.ConfigurationKey, ex.Message);
    }

    [Fact]
    public void BcryptPasswordHasher_BoundaryWorkFactor4_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BcryptPasswordHasher.ConfigurationKey] = "4" // minimum valid
            }).Build();

        var hash = BcryptPasswordHasher.Hash("password", config); // must not throw
        Assert.NotNull(hash);
    }

    // Work factor 14 hashes take ~1 s on modern hardware — acceptable for a unit test
    // suite but worth excluding from fast-feedback CI gates that impose tight budgets.
    // Run with: dotnet test --filter "Category!=Slow"
    [Trait("Category", "Slow")]
    [Fact]
    public void BcryptPasswordHasher_UpperBoundWorkFactor14_DoesNotThrow()
    {
        // Work factor 14 is a valid upper-range value (~1 s on modern hardware).
        // We do not test factor 31 (the absolute maximum) in automated tests because
        // BCrypt at factor 31 takes >5 minutes per hash, which is not suitable for CI.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BcryptPasswordHasher.ConfigurationKey] = "14"
            }).Build();

        var hash = BcryptPasswordHasher.Hash("password", config);
        Assert.NotNull(hash);
        Assert.Contains("$14$", hash);
    }

    [Fact]
    public void BcryptPasswordHasher_ConfigurationKeyIsCorrect()
    {
        // Sanity check: the constant used to read the config key matches
        // the appsettings.json path "Security:BcryptWorkFactor".
        Assert.Equal("Security:BcryptWorkFactor", BcryptPasswordHasher.ConfigurationKey);
    }

    [Fact]
    public void BcryptPasswordHasher_DefaultWorkFactorValue_Is12()
    {
        // DefaultWorkFactor = 12 is the production default — strong enough to take
        // ~250 ms on modern hardware, balancing security and login latency.
        Assert.Equal(12, BcryptPasswordHasher.DefaultWorkFactor);
    }
}
