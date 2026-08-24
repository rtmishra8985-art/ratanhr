using HRMS.Application.DTOs.Company;

namespace HRMS.Application.Interfaces;

public interface ICompanySettingsService
{
    Task<CompanySettingsDto?> GetSettingsAsync(int companyId);
    Task UpsertSettingsAsync(UpsertCompanySettingsDto dto);
}
