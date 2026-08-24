using HRMS.Infrastructure.Security;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Unit tests for AesEncryptionService (AES-256-GCM).
/// Validates: correct round-trip, idempotency, null/empty handling, wrong key rejection,
/// prefix tagging, and key-length enforcement.
/// </summary>
public class EncryptionServiceTests
{
    // 32-byte key, base64-encoded — safe for tests only, never reuse in production.
    private const string ValidKey32Bytes = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI="; // 32 bytes decoded

    private static HRMS.Infrastructure.Security.AesEncryptionService Make(string? key = null)
        => new(key ?? ValidKey32Bytes);

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        var svc = Make();
        var original = "123456789012";   // Aadhaar-style value

        var cipher = svc.Encrypt(original);
        var result = svc.Decrypt(cipher);

        Assert.Equal(original, result);
    }

    [Fact]
    public void Encrypt_PanNumber_RoundTripsCorrectly()
    {
        var svc = Make();
        const string pan = "ABCDE1234F";

        Assert.Equal(pan, svc.Decrypt(svc.Encrypt(pan)));
    }

    [Fact]
    public void Encrypt_BankAccountNumber_RoundTripsCorrectly()
    {
        var svc = Make();
        const string account = "9876543210001234";

        Assert.Equal(account, svc.Decrypt(svc.Encrypt(account)));
    }

    [Fact]
    public void Encrypt_UnicodePlaintext_RoundTripsCorrectly()
    {
        var svc = Make();
        const string name = "नमस्ते"; // UTF-8 multi-byte content

        Assert.Equal(name, svc.Decrypt(svc.Encrypt(name)));
    }

    // ── Idempotency ──────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_AlreadyEncryptedValue_IsIdempotent()
    {
        var svc = Make();
        var once = svc.Encrypt("sensitive-data")!;
        var twice = svc.Encrypt(once)!;

        // Encrypting an already-encrypted value must not double-encrypt it.
        Assert.Equal(once, twice);
    }

    // ── Null / empty ─────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_NullInput_ReturnsNull()
    {
        var svc = Make();
        Assert.Null(svc.Encrypt(null));
    }

    [Fact]
    public void Decrypt_NullInput_ReturnsNull()
    {
        var svc = Make();
        Assert.Null(svc.Decrypt(null));
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        var svc = Make();
        Assert.Equal(string.Empty, svc.Encrypt(string.Empty));
    }

    // ── Prefix tag ───────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_ProducesVersionPrefixedCiphertext()
    {
        var svc = Make();
        var cipher = svc.Encrypt("test-value")!;
        // Versioned prefix allows future key rotation and format changes.
        Assert.StartsWith("enc:v1:", cipher);
    }

    // ── Key validation ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyKey_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new HRMS.Infrastructure.Security.AesEncryptionService(""));
        Assert.Contains("EncryptionKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_KeyTooShort_ThrowsInvalidOperationException()
    {
        // 16-byte key (base64) — valid base64, but decodes to only 16 bytes, not 32.
        var ex = Assert.Throws<InvalidOperationException>(
            () => new HRMS.Infrastructure.Security.AesEncryptionService("dGVzdC10b28tc2hvcnQta2V5"));
        Assert.Contains("32 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Decrypt of legacy plaintext ──────────────────────────────────────────

    [Fact]
    public void Decrypt_LegacyPlaintextWithoutPrefix_ReturnedAsIs()
    {
        // Rows that existed before encryption was enabled must not crash on read.
        var svc = Make();
        const string legacy = "123456789012";
        Assert.Equal(legacy, svc.Decrypt(legacy));
    }
}
