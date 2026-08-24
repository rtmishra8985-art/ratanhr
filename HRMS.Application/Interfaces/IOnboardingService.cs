using HRMS.Application.Common;
using HRMS.Application.DTOs.Onboarding;

namespace HRMS.Application.Interfaces;

public interface IOnboardingService
{
    Task<List<OnboardingTemplateDto>> GetTemplatesAsync(int? companyId);
    Task<OnboardingTemplateDto> CreateTemplateAsync(int? companyId, CreateOnboardingTemplateDto dto);
    Task<bool> UpdateTemplateAsync(int id, int? companyId, CreateOnboardingTemplateDto dto);
    Task<bool> DeleteTemplateAsync(int id, int? companyId);
    Task<OnboardingRecordDto> AssignAsync(int? companyId, AssignOnboardingDto dto);
    Task<OnboardingRecordDto?> GetRecordAsync(string employeeId);
    Task<bool> MarkStepCompleteAsync(int recordId, string employeeId, MarkStepCompleteDto dto);
}
