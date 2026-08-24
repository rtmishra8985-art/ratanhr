using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.SuperAdmin;

public class CreateSuperAdminReq
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
}

public class StatusBody
{
    public bool IsActive { get; set; }
}
