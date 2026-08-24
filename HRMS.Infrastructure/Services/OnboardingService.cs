using System.Text.Json;
using HRMS.Application.Common;
using HRMS.Application.DTOs.Onboarding;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Onboarding;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class OnboardingService : IOnboardingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OnboardingService> _logger;

    public OnboardingService(ApplicationDbContext db, ILogger<OnboardingService> logger)
    {
        _db = db; _logger = logger;
    }

    public async Task<List<OnboardingTemplateDto>> GetTemplatesAsync(int? companyId)
    {
        var list = await _db.OnboardingTemplates
            .Where(t => t.IsActive && (companyId == null || t.CompanyId == null || t.CompanyId == companyId))
            .OrderBy(t => t.Name)
            .ToListAsync();
        return list.Select(ToTemplateDto).ToList();
    }

    public async Task<OnboardingTemplateDto> CreateTemplateAsync(int? companyId, CreateOnboardingTemplateDto dto)
    {
        var tpl = new OnboardingTemplate
        {
            CompanyId = companyId,
            Name      = dto.Name,
            Steps     = dto.Steps,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.OnboardingTemplates.Add(tpl);
        await _db.SaveChangesAsync();
        return ToTemplateDto(tpl);
    }

    public async Task<bool> UpdateTemplateAsync(int id, int? companyId, CreateOnboardingTemplateDto dto)
    {
        var tpl = await _db.OnboardingTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (tpl == null || !tpl.IsActive) return false;
        if (companyId.HasValue && tpl.CompanyId.HasValue && tpl.CompanyId != companyId) return false;
        tpl.Name = dto.Name; tpl.Steps = dto.Steps;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTemplateAsync(int id, int? companyId)
    {
        var tpl = await _db.OnboardingTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (tpl == null || !tpl.IsActive) return false;
        if (companyId.HasValue && tpl.CompanyId.HasValue && tpl.CompanyId != companyId) return false;
        // Soft-delete: mark inactive so it no longer appears in lists.
        tpl.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<OnboardingRecordDto> AssignAsync(int? companyId, AssignOnboardingDto dto)
    {
        // FIX AUDIT-07S-01 (cross-tenant write): the caller's tenant scope was previously
        // ignored — any admin could assign ANY template to ANY employee of ANY company.
        // Tenant context is derived from the authenticated principal (companyId argument,
        // null == SuperAdmin) and NEVER from the request body.
        var tpl = await _db.OnboardingTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == dto.TemplateId);
        if (tpl == null || !tpl.IsActive)
            throw new InvalidOperationException("Template not found.");

        // A company-scoped caller may only use their own templates or global (null) templates.
        if (companyId.HasValue && tpl.CompanyId.HasValue && tpl.CompanyId != companyId)
            throw new UnauthorizedAccessException("Template not found.");

        // The target employee must exist and belong to the caller's tenant.
        // IgnoreQueryFilters + explicit CompanyId compare so the check does not depend on
        // whichever tenant the ambient ITenantContext happens to carry.
        if (string.IsNullOrWhiteSpace(dto.EmployeeId))
            throw new ArgumentException("EmployeeId is required.");

        var employee = await _db.Employees
            .IgnoreQueryFilters()
            .Where(e => e.EmployeeCode == dto.EmployeeId)
            .Select(e => new { e.Id, e.CompanyId })
            .FirstOrDefaultAsync();
        // KeyNotFoundException -> 404 via ExceptionMiddleware (InvalidOperationException would 500).
        if (employee == null)
            throw new KeyNotFoundException("Employee not found.");

        var effectiveCompanyId = companyId ?? employee.CompanyId;
        if (employee.CompanyId != effectiveCompanyId)
            throw new UnauthorizedAccessException("Employee not found.");

        // Template and employee must resolve to the same tenant (covers the SuperAdmin path
        // where companyId is null and both identifiers are client-supplied).
        if (tpl.CompanyId.HasValue && tpl.CompanyId != effectiveCompanyId)
            throw new UnauthorizedAccessException("Template not found.");

        // AssignedTo, when supplied, must also be an employee of the same tenant.
        if (dto.AssignedTo.HasValue)
        {
            var assigneeCompanyId = await _db.Employees
                .IgnoreQueryFilters()
                .Where(e => e.Id == dto.AssignedTo.Value)
                .Select(e => (int?)e.CompanyId)
                .FirstOrDefaultAsync();
            if (assigneeCompanyId == null || assigneeCompanyId != effectiveCompanyId)
                throw new UnauthorizedAccessException("Assignee not found.");
        }

        var record = new OnboardingRecord
        {
            EmployeeId     = dto.EmployeeId,
            EmployeeFk     = employee.Id,
            TemplateId     = dto.TemplateId,
            CompletedSteps = "[]",
            AssignedTo     = dto.AssignedTo,
            DueDate        = dto.DueDate,
            CreatedAt      = DateTime.UtcNow
        };
        _db.OnboardingRecords.Add(record);
        await _db.SaveChangesAsync();
        record.Template = tpl;
        _logger.LogInformation(
            "Onboarding template {TemplateId} assigned to employee {EmployeeCode} in company {CompanyId}.",
            dto.TemplateId, dto.EmployeeId, effectiveCompanyId);
        return ToRecordDto(record);
    }


    public async Task<OnboardingRecordDto?> GetRecordAsync(string employeeId)
    {
        var record = await _db.OnboardingRecords
            .Include(r => r.Template)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.CompletedAt == null && r.DeletedAt == null);
        return record == null ? null : ToRecordDto(record);
    }

    public async Task<bool> MarkStepCompleteAsync(int recordId, string employeeId, MarkStepCompleteDto dto)
    {
        var record = await _db.OnboardingRecords.Include(r => r.Template).FirstOrDefaultAsync(r => r.Id == recordId);
        if (record == null || record.EmployeeId != employeeId) return false;

        var completed = JsonSerializer.Deserialize<List<int>>(record.CompletedSteps) ?? new List<int>();
        if (!completed.Contains(dto.StepIndex))
        {
            completed.Add(dto.StepIndex);
            record.CompletedSteps = JsonSerializer.Serialize(completed);

            // Check if all steps done
            var steps = JsonSerializer.Deserialize<List<object>>(record.Template?.Steps ?? "[]") ?? new List<object>();
            if (completed.Count >= steps.Count)
                record.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
        return true;
    }

    private static OnboardingTemplateDto ToTemplateDto(OnboardingTemplate t) => new()
    {
        Id        = t.Id,
        Name      = t.Name,
        Steps     = t.Steps,
        IsActive  = t.IsActive,
        CreatedAt = t.CreatedAt
    };

    private static OnboardingRecordDto ToRecordDto(OnboardingRecord r) => new()
    {
        Id             = r.Id,
        EmployeeId     = r.EmployeeId,
        TemplateId     = r.TemplateId,
        TemplateName   = r.Template?.Name ?? "",
        Steps          = r.Template?.Steps ?? "[]",
        CompletedSteps = r.CompletedSteps,
        AssignedTo     = r.AssignedTo,
        DueDate        = r.DueDate,
        CompletedAt    = r.CompletedAt,
        CreatedAt      = r.CreatedAt
    };
}
