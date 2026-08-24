namespace HRMS.Domain.Entities.Authentication;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "employee"; // superadmin | admin | employee
    public string? FullName { get; set; }
    public string? AdminRole { get; set; }
    public int? CompanyId { get; set; }
    public string? ProfilePicturePath { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Soft-delete flag. Soft-deleted users are excluded from all listings.</summary>
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>AES-256-GCM encrypted TOTP secret (Base32). Null when MFA not set up.</summary>
    public string? TotpSecret { get; set; }
    /// <summary>True only after user completes MFA setup confirmation.</summary>
    public bool IsMfaEnabled { get; set; } = false;

    /// <summary>
    /// True when this user account was created by the demo-mode seed service
    /// (<see cref="HRMS.Infrastructure.Services.Demo.DemoSeedService"/>). Used by
    /// CleanupAsync to delete only demo accounts and never touch real customer users.
    /// </summary>
    public bool IsDemo { get; set; } = false;
}
