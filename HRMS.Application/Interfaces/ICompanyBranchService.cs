using HRMS.Application.Common;
using HRMS.Application.DTOs.Company;

namespace HRMS.Application.Interfaces;

public interface ICompanyBranchService
{
    Task<List<CompanyBranchDto>> GetBranchesAsync(int companyId);
    Task<PagedResult<CompanyBranchDto>> GetBranchesPagedAsync(int companyId, int page, int pageSize);
    Task<CompanyBranchDto?> GetBranchAsync(int branchId, int callerCompanyId);
    Task<int> CreateBranchAsync(CreateCompanyBranchDto dto);
    Task<bool> UpdateBranchAsync(int branchId, int callerCompanyId, CreateCompanyBranchDto dto);
    Task<bool> DeleteBranchAsync(int branchId, int callerCompanyId);
}
