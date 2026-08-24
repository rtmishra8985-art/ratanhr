namespace HRMS.Domain.Enums;

/// <summary>Operational status of a registered biometric device.</summary>
public enum BiometricStatus
{
    Active      = 1,
    Inactive    = 2,
    Disabled    = 3,
    Unreachable = 4,
    Error       = 5,
}
