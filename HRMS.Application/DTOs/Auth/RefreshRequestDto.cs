namespace HRMS.Application.DTOs.Auth;

/// <summary>Body for /api/auth/refresh and /api/auth/logout.</summary>
public class RefreshRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
