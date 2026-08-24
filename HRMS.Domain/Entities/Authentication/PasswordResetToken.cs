namespace HRMS.Domain.Entities.Authentication;

/// <summary>
/// Real, single-use, time-limited password reset token (hashed at rest).
/// Replaces the earlier "email as token" placeholder.
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }

    public bool IsValid => UsedAt == null && ExpiresAt > DateTime.UtcNow;
}
