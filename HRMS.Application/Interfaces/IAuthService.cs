using HRMS.Application.DTOs.Auth;

namespace HRMS.Application.Interfaces;

public interface IAuthService
{
    Task<(LoginResponseDto? result, string? error)> LoginAsync(LoginDto dto, string? ipAddress = null);
    Task<bool> ForgotPasswordAsync(string email);
    Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task<UserProfileDto?> GetProfileAsync(int userId);
    Task<bool> UpdateProfileAsync(int userId, UpdateProfileDto dto);
    Task<bool> UpdateProfilePictureAsync(int userId, Microsoft.AspNetCore.Http.IFormFile file);
    Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(string refreshToken);

    /// <summary>
    /// Issues a new refresh token for <paramref name="userId"/> with <c>MfaVerified=true</c>.
    /// Must be called only after a successful TOTP verification (i.e. from MfaController.Verify).
    /// Returns the raw (unhashed) token to be set as an HttpOnly cookie by the caller.
    /// </summary>
    Task<string> IssueRefreshTokenAsync(int userId);
}
