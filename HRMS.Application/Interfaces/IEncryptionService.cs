namespace HRMS.Application.Interfaces;

/// <summary>
/// AES-256-GCM encryption service for PII fields stored at rest.
///
/// Encrypted values use the versioned wire-format:
///   enc:v1:&lt;base64(12-byte nonce ‖ 16-byte GCM tag ‖ ciphertext)&gt;
///
/// The "v1:" label allows future key rotation without re-encrypting all rows atomically:
/// old rows still carry v1 and can be re-encrypted lazily on next write.
///
/// Register as a singleton in DI. Apply to entity columns via
/// <c>EncryptedStringConverter</c> inside <c>ApplicationDbContext.OnModelCreating</c>.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns an <c>enc:v1:…</c> string.
    /// Returns <c>null</c> when input is <c>null</c>.
    /// Idempotent: already-encrypted values (prefix present) are returned unchanged.
    /// </summary>
    string? Encrypt(string? plaintext);

    /// <summary>
    /// Decrypts a value previously produced by <see cref="Encrypt"/>.
    /// Returns <c>null</c> when input is <c>null</c>.
    /// Legacy (unencrypted) rows — those lacking the <c>enc:v1:</c> prefix — are
    /// returned as-is, enabling a zero-downtime migration where rows are encrypted
    /// lazily on the next write.
    /// Throws <see cref="System.Security.Cryptography.CryptographicException"/> on
    /// key mismatch or corrupt ciphertext.
    /// </summary>
    string? Decrypt(string? ciphertext);

    /// <summary>
    /// Returns a masked display string safe for logs and non-privileged API responses.
    /// Example: "XXXX-XXXX-1234" for a 12-digit Aadhaar with <paramref name="visibleSuffix"/> = 4.
    /// Never throws; falls back to "****" on any error.
    /// </summary>
    string Mask(string? value, int visibleSuffix = 4);
}
