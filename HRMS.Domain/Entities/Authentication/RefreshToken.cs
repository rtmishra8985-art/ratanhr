using System.ComponentModel.DataAnnotations.Schema;
namespace HRMS.Domain.Entities.Authentication;

/// <summary>
/// Stores a hashed refresh token so the /auth/refresh endpoint can validate,
/// rotate, and revoke sessions instead of trusting an unpersisted GUID.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>Alias for TokenHash — tests use Token.</summary>
    [NotMapped] public string Token { get => TokenHash; set => TokenHash = value; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    /// <summary>
    /// Boolean alias for RevokedAt — tests use IsRevoked.
    /// Setting IsRevoked = true stamps RevokedAt with UtcNow; setting to false clears it.
    /// </summary>
    [NotMapped]
    public bool IsRevoked
    {
        get => RevokedAt.HasValue;
        set
        {
            if (value && !RevokedAt.HasValue) RevokedAt = DateTime.UtcNow;
            else if (!value) RevokedAt = null;
        }
    }
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>
    /// True when this token was issued after a successful TOTP verification.
    /// </summary>
    public bool MfaVerified { get; set; } = false;

    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
}
