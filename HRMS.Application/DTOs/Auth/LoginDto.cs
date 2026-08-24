using System.Text.Json.Serialization;

namespace HRMS.Application.DTOs.Auth;

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Portal { get; set; } = "employee"; // employee | admin | superadmin
    public string? AdminRole { get; set; }
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    [JsonIgnore]  // Delivered via HttpOnly cookie — not serialised to response body
    public string RefreshToken { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int? CompanyId { get; set; }
    public string? EmployeeId { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime ExpiresAt { get; set; }
    /// <summary>
    /// When true the caller must complete the MFA step at POST /api/auth/mfa/verify
    /// using <see cref="TempToken"/> before a full session is granted.
    /// All other fields except UserId and FullName will be empty/default.
    /// </summary>
    public bool MfaRequired { get; set; } = false;
    /// <summary>Short-lived token (10 min) used exclusively for the MFA verify step.</summary>
    public string? TempToken { get; set; }
}

public class ForgotPasswordDto
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
