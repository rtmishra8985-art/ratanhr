using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HRMS.API.Controllers.Authentication;

[ApiController]
[Route("api/auth/mfa")]
[Authorize]
public class MfaController : BaseController
{
    private readonly IMfaService _mfa;
    public MfaController(IMfaService mfa) => _mfa = mfa;

    /// <summary>Step 1: Start MFA setup — returns QR code URI</summary>
    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
    {
        var result = await _mfa.SetupMfaAsync(UserId);
        return Ok(ApiResponse<MfaSetupResponseDto>.Ok(result));
    }

    /// <summary>Step 2: Confirm MFA setup with a 6-digit code</summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmMfaDto dto)
    {
        var ok = await _mfa.ConfirmMfaSetupAsync(UserId, dto.Code);
        return ok
            ? Ok(ApiResponse.Ok("MFA enabled successfully."))
            : BadRequest(ApiResponse.Fail("Invalid or expired code. Please try again."));
    }

    /// <summary>
    /// Called after password login when mfaRequired=true.
    /// Validates the TOTP code and returns a full JWT on success.
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    // FIX P1: TOTP verify was only covered by the generic 120 req/min "api" policy,
    // which permits sustained 6-digit code guessing. Use the sensitive-auth limiter.
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyMfaDto dto,
        [FromServices] IAuthService auth,
        [FromServices] HRMS.Application.Interfaces.IJwtService jwt)
    {
        // Validate temp token — for simplicity we validate it as a bearer and extract userId
        var principal = jwt.ValidateTempToken(dto.TempToken);
        if (principal == null)
            return Unauthorized(ApiResponse.Fail("Invalid or expired temporary token."));

        var userIdStr = principal.FindFirst("sub")?.Value ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(ApiResponse.Fail("Invalid token."));

        var ok = await _mfa.VerifyMfaAsync(userId, dto.Code);
        if (!ok) return BadRequest(ApiResponse.Fail("Invalid TOTP code."));

        var profile = await auth.GetProfileAsync(userId);
        if (profile == null) return Unauthorized(ApiResponse.Fail("User not found."));

        // Issue full JWT — store in HttpOnly cookie (XSS-safe), not the response body.
        // This is consistent with AuthController.Login which uses SetAccessTokenCookie().
        var user = new Domain.Entities.Authentication.User
        {
            Id = userId, Email = profile.Email!, Role = profile.Role!, FullName = profile.FullName,
            AdminRole = profile.AdminRole, CompanyId = profile.CompanyId
        };
        var token = jwt.GenerateToken(user, profile.EmployeeId);
        SetAccessTokenCookie(token);

        // FIX [2]: Issue and persist a refresh token for this TOTP-verified session.
        // Without this the access token can never be silently renewed — the user must
        // re-authenticate from scratch when the 12-hour cookie expires.
        // IssueRefreshTokenAsync sets MfaVerified=true on the stored record so that
        // RefreshTokenAsync will not reject it for MFA-enabled accounts (Fix [1]).
        var refreshRaw = await auth.IssueRefreshTokenAsync(userId);
        SetRefreshTokenCookie(refreshRaw);

        return Ok(ApiResponse.Ok("MFA verification successful. Authentication complete."));
    }

    /// <summary>Disable MFA (requires current password for confirmation)</summary>
    [HttpDelete]
    public async Task<IActionResult> Disable([FromBody] DisableMfaDto dto)
    {
        var ok = await _mfa.DisableMfaAsync(UserId, dto.CurrentPassword);
        return ok
            ? Ok(ApiResponse.Ok("MFA disabled."))
            : BadRequest(ApiResponse.Fail("Incorrect current password."));
    }
}
