using HRMS.Application.DTOs.AdminUsers;

namespace HRMS.Application.Interfaces;

/// <summary>
/// Service interface for admin-user management: CRUD, role assignment,
/// password reset, and company-scoped access control.
/// </summary>
public interface IAdminUserService
{
    Task<List<AdminUserDto>> GetAdminsByCompanyAsync(int companyId);
    Task<AdminUserDto?> GetAdminByIdAsync(int userId, int companyId);
    Task<int> CreateAdminAsync(int companyId, CreateAdminUserDto dto);
    Task<bool> AssignRoleAsync(int userId, int companyId, string role);
    Task<bool> ResetPasswordAsync(int userId, int companyId, string newPassword);
}
