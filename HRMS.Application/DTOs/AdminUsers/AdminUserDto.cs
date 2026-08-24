using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.DTOs.AdminUsers;

public class AdminUserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? AdminRole { get; set; }
    public int? CompanyId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>DTO used by IAdminUserService (and its unit tests) to create an admin user.</summary>
public class CreateAdminUserDto
{
    public string Email    { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role     { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class CreateAdminUserRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
    public string? AdminRole { get; set; }
    public int? CompanyId { get; set; }
}

public class UpdateAdminUserRequest
{
    public string? FullName { get; set; }
    public string? AdminRole { get; set; }
    public int? CompanyId { get; set; }
    /// <summary>Leave null/empty to keep the existing password.</summary>
    public string? NewPassword { get; set; }
}

public class UpdateStatusReq
{
    public bool IsActive { get; set; }
}
