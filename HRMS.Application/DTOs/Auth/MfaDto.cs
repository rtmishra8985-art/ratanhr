namespace HRMS.Application.DTOs.Auth;

public class MfaSetupResponseDto
{
    public string QrCodeUri { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
}

public class ConfirmMfaDto
{
    public string Code { get; set; } = string.Empty;
}

public class VerifyMfaDto
{
    public string TempToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class DisableMfaDto
{
    public string CurrentPassword { get; set; } = string.Empty;
}
