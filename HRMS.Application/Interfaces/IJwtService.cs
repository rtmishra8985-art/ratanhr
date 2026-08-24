using System.Security.Claims;
using HRMS.Domain.Entities.Authentication;

namespace HRMS.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user, string? employeeId = null);
    int? ValidateToken(string token);
    /// <summary>Issues a short-lived (5 min) temp token used during MFA verification step.</summary>
    string GenerateTempToken(int userId);
    /// <summary>Validates a temp token and returns its ClaimsPrincipal, or null if invalid.</summary>
    ClaimsPrincipal? ValidateTempToken(string token);
}
