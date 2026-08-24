using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;
using Microsoft.AspNetCore.Http;

namespace HRMS.Application.Interfaces;

public interface ICompanyService
{
    Task<int> CreateAsync(CreateCompanyDto dto);
    Task<bool> UpdateAsync(int id, CreateCompanyDto dto);
    Task<bool> UpdateLogoAsync(int id, IFormFile logo);
    Task<CompanyDto?> GetByIdAsync(int id);
    Task<List<CompanyDto>> GetAllAsync();
    Task<PagedResult<CompanyDto>> GetAllPagedAsync(int page, int pageSize);
    Task<bool> DeleteAsync(int id);
}
