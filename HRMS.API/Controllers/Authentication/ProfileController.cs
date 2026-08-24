using HRMS.Application.Common;
using HRMS.Application.DTOs.Auth;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using HRMS.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HRMS.API.Controllers.Authentication;

[ApiController]
[Route("api/profile")]
[Authorize(Policy = "RequireMfaCompleted")]
public class ProfileController : BaseController
{
    private readonly IAuthService _auth;

    public ProfileController(IAuthService auth) => _auth = auth;

    private new int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get current user profile</summary>
    // FIX LOW: Added ProducesResponseType attributes for Swagger contract clarity and
    // server-side authorization documentation. The class-level authorization attribute ensures all endpoints
    // require a valid JWT — explicit documentation of the 401/403 paths assists API consumers.
    [HttpGet]
    [ProducesResponseType(typeof(HRMS.Application.Common.ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await _auth.GetProfileAsync(UserId);
        if (profile == null) return NotFound(ApiResponse.Fail("User not found."));
        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    /// <summary>Update display name</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var ok = await _auth.UpdateProfileAsync(UserId, dto);
        return ok ? Ok(ApiResponse.Ok("Profile updated."))
                  : NotFound(ApiResponse.Fail("User not found."));
    }

    /// <summary>Upload / replace profile picture (JPEG or PNG, max 5 MB)</summary>
    [HttpPost("picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadPicture(IFormFile file)
    {
        // Audit item 9 — the ad-hoc Content-Type allow-list and hand-rolled 5 MB check
        // were replaced by the shared UploadValidator (Image profile: extension
        // allow-list, declared-MIME/extension agreement, magic-byte signature, 5 MB
        // ceiling). A renamed .exe announced as image/png is now rejected with 400.
        var upload = UploadValidator.Validate(file, UploadProfile.Image);
        if (!upload.IsValid) return BadRequest(ApiResponse.Fail(upload.Error!));

        var ok = await _auth.UpdateProfilePictureAsync(UserId, file);
        return ok ? Ok(ApiResponse.Ok("Profile picture updated."))
                  : NotFound(ApiResponse.Fail("User not found."));
    }
}
