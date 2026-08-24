namespace HRMS.Domain.Enums;

/// <summary>
/// Identifies which biometric hardware vendor/integration type a device uses.
/// Must match provider VendorName strings in IBiometricProvider implementations.
/// </summary>
public enum BiometricProviderType
{
    ZKTeco    = 1,
    ESSL      = 2,
    Matrix    = 3,
    Suprema   = 4,
    Hikvision = 5,
    Anviz     = 6,
    Realtime  = 7,
    Future    = 8,
}
