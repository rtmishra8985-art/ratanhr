using HRMS.Application.DTOs.Auth;

namespace HRMS.Application.Interfaces;

public interface IMfaService
{
    Task<MfaSetupResponseDto> SetupMfaAsync(int userId);
    Task<bool> ConfirmMfaSetupAsync(int userId, string code);
    Task<bool> VerifyMfaAsync(int userId, string code);
    Task<bool> DisableMfaAsync(int userId, string currentPassword);
    bool IsMfaEnabled(int userId);
}
