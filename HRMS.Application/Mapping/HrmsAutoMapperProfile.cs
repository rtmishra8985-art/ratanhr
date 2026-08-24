using AutoMapper;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.DTOs.Department;
using HRMS.Application.DTOs.Employee;
using HRMS.Application.DTOs.Holiday;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.DTOs.Payroll;
using HRMS.Application.DTOs.Timesheet;
using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Timesheet;

namespace HRMS.Application.Mapping;

public class HrmsAutoMapperProfile : Profile
{
    public HrmsAutoMapperProfile()
    {
        // ── Employee ──────────────────────────────────────────────────────

        // Employee list projection (lightweight — used in paginated tables)
        CreateMap<Employee, EmployeeListDto>()
            .ForMember(d => d.EmployeeId,    o => o.MapFrom(s => s.EmployeeId))
            .ForMember(d => d.CompanyId,     o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.FullName,      o => o.MapFrom(s => s.DisplayName))
            .ForMember(d => d.Department,    o => o.MapFrom(s => s.Department ?? string.Empty))
            .ForMember(d => d.Designation,   o => o.MapFrom(s => s.Designation ?? string.Empty))
            .ForMember(d => d.Status,        o => o.MapFrom(s => s.Status))
            .ForMember(d => d.IsActive,      o => o.MapFrom(s => s.IsActive))
            .ForMember(d => d.PassportPhoto, o => o.MapFrom(s => s.PassportPhoto))
            .ForMember(d => d.Gender,        o => o.MapFrom(s => s.Gender))
            // Doj is a legacy string field; Employee stores DateOfJoining (DateOnly?)
            .ForMember(d => d.Doj,           o => o.Ignore());

        // Employee detail view — FIX MED-9: PII fields are explicitly ignored so they
        // are never populated in the standard detail response regardless of the source entity.
        // PII is returned only via the EmployeePiiDto mapping below.
        CreateMap<Employee, EmployeeDetailDto>()
            .ForMember(d => d.EmployeeId,      o => o.MapFrom(s => s.EmployeeId.ToString()))
            .ForMember(d => d.FullName,        o => o.MapFrom(s => s.DisplayName))
            .ForMember(d => d.Department,      o => o.MapFrom(s => s.Department ?? string.Empty))
            .ForMember(d => d.Designation,     o => o.MapFrom(s => s.Designation ?? string.Empty))
            .ForMember(d => d.CompanyId,       o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.PassportPhoto,   o => o.MapFrom(s => s.PassportPhoto))
            .ForMember(d => d.IdentityDocs,    o => o.MapFrom(s => s.IdentityDocs))
            .ForMember(d => d.EducationalDocs, o => o.MapFrom(s => s.EducationalDocs))
            .ForMember(d => d.ExperienceDocs,  o => o.MapFrom(s => s.ExperienceDocs))
            .ForMember(d => d.CreatedAt,       o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.Uan,             o => o.MapFrom(s => s.UAN))
            // Legacy string date fields — Employee uses DateOnly? typed properties instead
            .ForMember(d => d.Dob,             o => o.Ignore())
            .ForMember(d => d.Doj,             o => o.Ignore())
            // MED-9: PII — never map these to the standard detail DTO
            .ForMember(d => d.Aadhaar,         o => o.Ignore())
            .ForMember(d => d.Pan,             o => o.Ignore())
            .ForMember(d => d.AccountNumber,   o => o.Ignore())
            .ForMember(d => d.IfscCode,        o => o.Ignore());

        // MED-9: PII-gated mapping — only used by GET /api/employees/{id}/pii (PII_VIEWER role)
        CreateMap<Employee, EmployeePiiDto>()
            .ForMember(d => d.EmployeeId,          o => o.MapFrom(s => s.EmployeeId))
            .ForMember(d => d.AadhaarMasked,       o => o.MapFrom(s => MaskAadhaar(s.Aadhaar)))
            .ForMember(d => d.PanMasked,           o => o.MapFrom(s => MaskPan(s.PAN)))
            .ForMember(d => d.AccountNumberMasked, o => o.MapFrom(s => MaskAccount(s.AccountNumber)))
            .ForMember(d => d.IFSCCode,            o => o.MapFrom(s => s.IFSCCode))
            .ForMember(d => d.UAN,                 o => o.MapFrom(s => s.UAN))
            .ForMember(d => d.Raw,                 o => o.Ignore());

        // ── Department / Designation ──────────────────────────────────────

        CreateMap<Department, DepartmentDto>()
            .ForMember(d => d.Id,           o => o.MapFrom(s => s.Id))
            .ForMember(d => d.DepartmentId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.CompanyId,    o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.Name,         o => o.MapFrom(s => s.Name))
            .ForMember(d => d.Description,  o => o.MapFrom(s => s.Description))
            .ForMember(d => d.IsActive,     o => o.MapFrom(s => s.IsActive))
            .ForMember(d => d.CreatedAt,    o => o.MapFrom(s => s.CreatedAt));

        CreateMap<CreateDepartmentDto, Department>()
            .ForMember(d => d.Id,           opt => opt.Ignore())
            .ForMember(d => d.DepartmentId, opt => opt.Ignore())
            .ForMember(d => d.CompanyId,    opt => opt.Ignore())
            .ForMember(d => d.IsActive,     opt => opt.Ignore())
            .ForMember(d => d.CreatedAt,    opt => opt.Ignore());

        CreateMap<Designation, DesignationDto>()
            .ForMember(d => d.Id,          o => o.MapFrom(s => s.Id))
            .ForMember(d => d.CompanyId,   o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.Name,        o => o.MapFrom(s => s.Name))
            .ForMember(d => d.Description, o => o.MapFrom(s => s.Description))
            .ForMember(d => d.IsActive,    o => o.MapFrom(s => s.IsActive))
            .ForMember(d => d.CreatedAt,   o => o.MapFrom(s => s.CreatedAt));

        CreateMap<CreateDesignationDto, Designation>()
            .ForMember(d => d.Id,        opt => opt.Ignore())
            .ForMember(d => d.CompanyId, opt => opt.Ignore())
            .ForMember(d => d.IsActive,  opt => opt.Ignore())
            .ForMember(d => d.CreatedAt, opt => opt.Ignore());

        // ── Leave ─────────────────────────────────────────────────────────

        CreateMap<LeaveType, LeaveTypeDto>()
            .ForMember(d => d.Id,              o => o.MapFrom(s => s.Id))
            .ForMember(d => d.LeaveTypeId,     o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Name,            o => o.MapFrom(s => s.Name))
            .ForMember(d => d.AnnualQuotaDays, o => o.MapFrom(s => s.AnnualQuotaDays))
            .ForMember(d => d.Quota,           o => o.MapFrom(s => s.AnnualQuotaDays))
            .ForMember(d => d.IsPaid,          o => o.MapFrom(s => s.IsPaid))
            .ForMember(d => d.IsActive,        o => o.MapFrom(s => s.IsActive));

        CreateMap<CreateLeaveTypeDto, LeaveType>()
            .ForMember(d => d.Id,          opt => opt.Ignore())
            .ForMember(d => d.LeaveTypeId, opt => opt.Ignore())
            .ForMember(d => d.CompanyId,   opt => opt.Ignore())
            .ForMember(d => d.IsActive,    opt => opt.Ignore())
            .ForMember(d => d.CreatedAt,   opt => opt.Ignore());

        // LeaveRequest → LeaveRequestDto
        CreateMap<LeaveRequest, LeaveRequestDto>()
            .ForMember(d => d.Id,              o => o.MapFrom(s => s.Id))
            .ForMember(d => d.LeaveRequestId,  o => o.MapFrom(s => s.Id))
            .ForMember(d => d.EmployeeId,      o => o.MapFrom(s => s.EmployeeId))
            .ForMember(d => d.CompanyId,       o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.LeaveTypeId,     o => o.MapFrom(s => s.LeaveTypeId))
            .ForMember(d => d.TotalDays,       o => o.MapFrom(s => s.TotalDays))
            .ForMember(d => d.Reason,          o => o.MapFrom(s => s.Reason))
            .ForMember(d => d.Status,          o => o.MapFrom(s => s.Status))
            .ForMember(d => d.ApproverRemarks, o => o.MapFrom(s => s.ApproverRemarks))
            .ForMember(d => d.CreatedAt,       o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.StartDate,       o => o.MapFrom(s => s.StartDate.ToString("yyyy-MM-dd")))
            .ForMember(d => d.EndDate,         o => o.MapFrom(s => s.EndDate.ToString("yyyy-MM-dd")))
            // Populated separately by service layer (join on LeaveType / Employee)
            .ForMember(d => d.EmployeeName,    o => o.Ignore())
            .ForMember(d => d.LeaveTypeName,   o => o.Ignore());

        // ── Payroll ───────────────────────────────────────────────────────

        // Payslip — guard against invalid Month/Year values before calling DateTime constructor.
        // Payslip.Month and Payslip.Year are stored as integers; a corrupted row could carry 0
        // or an out-of-range value that would throw inside new DateTime(year, month, 1).
        CreateMap<Payslip, PayslipDto>()
            .ForMember(d => d.Id,            o => o.MapFrom(s => s.Id))
            .ForMember(d => d.PayslipId,     o => o.MapFrom(s => s.Id))
            .ForMember(d => d.CompanyId,     o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.Status,        o => o.MapFrom(s => s.Status))
            .ForMember(d => d.MonthYear,     o => o.MapFrom(s => SafeMonthYear(s.Year, s.Month)))
            .ForMember(d => d.PFEmployee,    o => o.MapFrom(s => s.PFEmployee))
            .ForMember(d => d.PFEmployer,    o => o.MapFrom(s => s.PFEmployer))
            .ForMember(d => d.GrossEarnings, o => o.MapFrom(s => s.GrossEarnings))
            .ForMember(d => d.GrossPay,      o => o.MapFrom(s => s.GrossEarnings))
            .ForMember(d => d.NetPay,        o => o.MapFrom(s => s.NetPay))
            .ForMember(d => d.NetSalary,     o => o.MapFrom(s => s.NetPay))
            // Fields not on the Payslip entity — populated by service layer
            .ForMember(d => d.EmployeeName,  o => o.Ignore())
            .ForMember(d => d.Designation,   o => o.Ignore())
            .ForMember(d => d.Department,    o => o.Ignore())
            .ForMember(d => d.BankName,      o => o.Ignore())
            .ForMember(d => d.AccountNumber, o => o.Ignore())
            .ForMember(d => d.UAN,           o => o.Ignore())
            .ForMember(d => d.CompanyName,   o => o.Ignore())
            .ForMember(d => d.CompanyLogo,   o => o.Ignore());

        // ── Timesheet ─────────────────────────────────────────────────────

        CreateMap<TimesheetEntry, TimesheetEntryDto>().ReverseMap();
        CreateMap<CreateTimesheetDto, TimesheetEntry>()
            .ForMember(d => d.Status,           o => o.MapFrom(_ => "Draft"))
            .ForMember(d => d.CreatedAt,        o => o.MapFrom(_ => DateTime.UtcNow))
            .ForMember(d => d.UpdatedAt,        o => o.MapFrom(_ => DateTime.UtcNow))
            // Server-assigned fields — never sourced from the incoming DTO
            .ForMember(d => d.Id,               o => o.Ignore())
            .ForMember(d => d.CompanyId,        o => o.Ignore())
            .ForMember(d => d.ManagerRemarks,   o => o.Ignore())
            .ForMember(d => d.ApprovedByUserId, o => o.Ignore())
            .ForMember(d => d.ApprovedAt,       o => o.Ignore());

        // ── Timesheet weekly aggregate ────────────────────────────────────

        CreateMap<Timesheet, TimesheetDto>()
            .ForMember(d => d.TimesheetId, o => o.MapFrom(s => s.TimesheetId));

        // ── WebAttendance ─────────────────────────────────────────────────

        CreateMap<WebAttendance, AttendanceDto>()
            .ForMember(d => d.AttendanceId, o => o.MapFrom(s => s.AttendanceId))
            .ForMember(d => d.EmployeeId,   o => o.MapFrom(s => s.EmployeeId))
            .ForMember(d => d.CompanyId,    o => o.MapFrom(s => s.CompanyId))
            .ForMember(d => d.Date,         o => o.MapFrom(s => s.Date))
            .ForMember(d => d.CheckIn,      o => o.MapFrom(s => s.CheckIn))
            .ForMember(d => d.CheckOut,     o => o.MapFrom(s => s.CheckOut))
            .ForMember(d => d.Status,       o => o.MapFrom(s => s.Status))
            .ForMember(d => d.CreatedAt,    o => o.MapFrom(s => s.CreatedAt));
    }

    // FIX MED-9: PII masking helpers — return masked representation for EmployeePiiDto.
    // Raw values are only returned by the controller when caller holds PII_VIEWER and
    // explicitly requests unmasking.
    private static string? MaskAadhaar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 4) return value;
        return new string('*', value.Length - 4) + value[^4..];
    }

    private static string? MaskPan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 4) return value;
        return new string('*', value.Length - 4) + value[^4..];
    }

    private static string? MaskAccount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 4) return value;
        return new string('*', value.Length - 4) + value[^4..];
    }

    /// <summary>
    /// Safely converts a payslip Year + Month to a display string.
    /// Returns a descriptive fallback instead of throwing when the stored values
    /// fall outside the valid DateTime range (month 1–12, year 1–9999).
    /// </summary>
    private static string SafeMonthYear(int year, int month)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
            return $"Period {year}/{month} (invalid)";

        try
        {
            return new DateTime(year, month, 1).ToString("MMMM yyyy");
        }
        catch (ArgumentOutOfRangeException)
        {
            return $"Period {year}/{month} (invalid)";
        }
    }
}
