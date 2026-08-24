using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMS.API.Controllers;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Authentication;

[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Login – Employee, Admin, or SuperAdmin portal</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var ip = Request.Headers["X-Real-IP"].FirstOrDefault()
              ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
              ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        var (result, error) = await _auth.LoginAsync(dto, ip);
        if (result == null)
            return Unauthorized(ApiResponse.Fail(error ?? "Invalid credentials."));

        // Set both tokens as HttpOnly cookies (XSS-safe).
        SetAccessTokenCookie(result.Token);
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(ApiResponse<LoginResponseDto>.Ok(result, "Login successful"));
    }

    /// <summary>Exchange a valid refresh token for a new access + refresh token pair</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto)
    {
        // Refresh token MUST come from the HttpOnly cookie only.
        // Body fallback removed — accepting tokens from the body bypasses the HttpOnly
        // cookie security boundary and exposes the token to JavaScript-accessible storage.
        var refreshToken = Request.Cookies["hrms_refresh_token"];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(ApiResponse.Fail("Refresh token missing."));

        var result = await _auth.RefreshTokenAsync(refreshToken);
        if (result == null)
            return Unauthorized(ApiResponse.Fail("Invalid or expired refresh token. Please log in again."));

        SetAccessTokenCookie(result.Token);
        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(ApiResponse<LoginResponseDto>.Ok(result, "Token refreshed"));
    }

    /// <summary>Revoke a refresh token (logout on this device)</summary>
    [HttpPost("logout")]
    // Authorization audit: [AllowAnonymous] is retained deliberately. Logout must
    // still succeed when the access token has already expired or been revoked —
    // otherwise the browser keeps stale HttpOnly cookies it cannot clear itself.
    // It is not an information-disclosure or cross-user risk: the only token acted
    // on is the one in the caller's own hrms_refresh_token cookie (no body/query
    // fallback), an unknown token is a silent no-op, and the response is a constant
    // message that never reveals whether a session existed.
    [AllowAnonymous]
    // Audit fix: previously the only anonymous endpoint with no explicit limiter,
    // so it inherited the permissive 120 req/min "api" default. Pinned to the
    // "login" bucket (10/min per IP) — ample for real logouts, and it stops an
    // unauthenticated caller using it as a cheap unmetered endpoint.
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Logout()
    {
        // Refresh tokens are accepted from the HttpOnly cookie only. Do not
        // accept a request-body fallback: JavaScript-readable request bodies
        // would bypass the cookie security boundary.
        var refreshToken = Request.Cookies["hrms_refresh_token"];
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _auth.LogoutAsync(refreshToken);

        // Clear both HttpOnly cookies
        Response.Cookies.Delete("hrms_access_token", new CookieOptions
        {
            HttpOnly = true,
            Secure   = IsSecureCookieContext,
            SameSite = SameSiteMode.Strict,
            Path     = "/"
        });
        Response.Cookies.Delete("hrms_refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure   = IsSecureCookieContext,
            SameSite = SameSiteMode.Strict,
            Path     = "/api/auth"
        });

        return Ok(ApiResponse.Ok("Logged out."));
    }

    /// <summary>Forgot password — issues a one-time reset link sent via email</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _auth.ForgotPasswordAsync(dto.Email);
        return Ok(ApiResponse.Ok("If that email is registered, a password reset link has been sent."));
    }

    /// <summary>Reset password using the token from the email link</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword)
            return BadRequest(ApiResponse.Fail("Passwords do not match."));
        // Item 8: explicit policy check so the caller gets an actionable message
        // (AuthService re-checks it as the authoritative gate).
        if (!PasswordPolicy.IsValid(dto.NewPassword, out var pwError))
            return BadRequest(ApiResponse.Fail(pwError!));
        var ok = await _auth.ResetPasswordAsync(dto);
        return ok ? Ok(ApiResponse.Ok("Password reset successfully."))
                  : BadRequest(ApiResponse.Fail("Invalid or expired reset token."));
    }

    /// <summary>Change password (authenticated)</summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = UserId;
        // Item 8: server-side complexity gate before touching the service.
        if (!PasswordPolicy.IsValid(dto.NewPassword, out var pwError))
            return BadRequest(ApiResponse.Fail(pwError!));
        var ok = await _auth.ChangePasswordAsync(userId, dto);
        return ok ? Ok(ApiResponse.Ok("Password changed."))
                  : BadRequest(ApiResponse.Fail(
                        "Current password is incorrect, or the new password does not meet the password policy."));
    }

}
