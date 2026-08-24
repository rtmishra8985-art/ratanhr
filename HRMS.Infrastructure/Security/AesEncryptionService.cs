using System.Security.Cryptography;
using HRMS.Application.Interfaces;

namespace HRMS.Infrastructure.Security;

/// <summary>
/// App-level AES-256-GCM encryption for sensitive PII columns (Aadhaar, PAN, bank account
/// number) so plaintext never lands in the database or in a backup dump.
/// Key comes from configuration ("Security:EncryptionKey" / ENCRYPTION_KEY env var) — never
/// hardcoded. Must be a 32-byte value, base64-encoded.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private const string Prefix = "enc:v1:";
    private readonly byte[] _key;

    public AesEncryptionService(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(
                "Security:EncryptionKey is not configured. Generate one with " +
                "`openssl rand -base64 32` and set it via the ENCRYPTION_KEY environment variable.");

        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != 32)
            throw new InvalidOperationException("Security:EncryptionKey must decode to exactly 32 bytes (AES-256).");
    }

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        if (plaintext.StartsWith(Prefix)) return plaintext; // already encrypted (idempotent)

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string? Decrypt(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return stored;
        if (!stored.StartsWith(Prefix)) return stored; // legacy plaintext row, tolerate during migration

        var payload = Convert.FromBase64String(stored.Substring(Prefix.Length));
        var nonce = payload[..12];
        var tag = payload[12..28];
        var cipherBytes = payload[28..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }

    /// <inheritdoc/>
    public string Mask(string? value, int visibleSuffix = 4)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return "****";
            // Decrypt first if the value is encrypted so we mask the plaintext length
            var plain = value.StartsWith(Prefix) ? Decrypt(value) : value;
            if (string.IsNullOrEmpty(plain) || plain.Length <= visibleSuffix)
                return "****";
            return new string('X', plain.Length - visibleSuffix) + plain[^visibleSuffix..];
        }
        catch
        {
            return "****";
        }
    }
}
