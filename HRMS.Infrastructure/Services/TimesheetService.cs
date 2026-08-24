using HRMS.Application.Common;
using HRMS.Application.DTOs.Timesheet;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Timesheet;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class TimesheetService : ITimesheetService
{
    private readonly ApplicationDbContext _db;

    public TimesheetService(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<TimesheetEntryDto>> GetByEmployeeAsync(
        string employeeId, int companyId, PaginationQuery q)
    {
        var query = _db.TimesheetEntries
            .Where(t => t.EmployeeId == employeeId && t.CompanyId == companyId)
            .OrderByDescending(t => t.WorkDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(t => ToDto(t))
            .ToListAsync();

        return PagedResult<TimesheetEntryDto>.Create(items, total, q.Page, q.PageSize);
    }

    public async Task<PagedResult<TimesheetEntryDto>> GetPendingApprovalsAsync(
        int companyId, PaginationQuery q)
    {
        var query = _db.TimesheetEntries
            .Where(t => t.CompanyId == companyId && t.Status == "Submitted")
            .OrderByDescending(t => t.WorkDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(t => ToDto(t))
            .ToListAsync();

        return PagedResult<TimesheetEntryDto>.Create(items, total, q.Page, q.PageSize);
    }

    public async Task<TimesheetEntryDto> CreateAsync(CreateTimesheetDto dto, int companyId)
    {
        var entry = new TimesheetEntry
        {
            CompanyId       = companyId,
            EmployeeId      = dto.EmployeeId,
            WorkDate        = dto.WorkDate,
            ProjectCode     = dto.ProjectCode,
            TaskDescription = dto.TaskDescription,
            HoursWorked     = dto.HoursWorked,
            Status          = "Draft",
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };
        _db.TimesheetEntries.Add(entry);
        await _db.SaveChangesAsync();
        return ToDto(entry);
    }

    public async Task<TimesheetEntryDto> UpdateAsync(int id, CreateTimesheetDto dto, string employeeId)
    {
        var entry = await _db.TimesheetEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Timesheet entry not found.");

        if (entry.Status != "Draft")
            throw new InvalidOperationException("Only Draft entries can be edited.");

        entry.ProjectCode     = dto.ProjectCode;
        entry.TaskDescription = dto.TaskDescription;
        entry.HoursWorked     = dto.HoursWorked;
        entry.WorkDate        = dto.WorkDate;
        entry.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(entry);
    }

    public async Task SubmitAsync(int id, string employeeId)
    {
        var entry = await _db.TimesheetEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Timesheet entry not found.");

        if (entry.Status != "Draft")
            throw new InvalidOperationException("Only Draft entries can be submitted.");

        entry.Status    = "Submitted";
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // FIX 2: ApproveAsync and RejectAsync now require companyId and verify the
    // entry belongs to the caller's company before mutating it. Previously both
    // used FindAsync(id) with no company scope — a Company-A admin could approve or
    // reject Company-B timesheets by guessing the sequential integer ID.
    public async Task ApproveAsync(int id, int approverUserId, int companyId, string? remarks)
    {
        var entry = await _db.TimesheetEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Timesheet entry not found.");

        entry.Status          = "Approved";
        entry.ApprovedByUserId= approverUserId;
        entry.ApprovedAt      = DateTime.UtcNow;
        entry.ManagerRemarks  = remarks;
        entry.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int id, int approverUserId, int companyId, string remarks)
    {
        var entry = await _db.TimesheetEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId)
            ?? throw new KeyNotFoundException("Timesheet entry not found.");

        entry.Status          = "Rejected";
        entry.ManagerRemarks  = remarks;
        entry.ApprovedByUserId= approverUserId;
        entry.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, string employeeId)
    {
        var entry = await _db.TimesheetEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Timesheet entry not found.");

        if (entry.Status != "Draft")
            throw new InvalidOperationException("Only Draft entries can be deleted.");

        _db.TimesheetEntries.Remove(entry);
        await _db.SaveChangesAsync();
    }

    private static TimesheetEntryDto ToDto(TimesheetEntry t) => new()
    {
        Id              = t.Id,
        CompanyId       = t.CompanyId,
        EmployeeId      = t.EmployeeId,
        WorkDate        = t.WorkDate,
        ProjectCode     = t.ProjectCode,
        TaskDescription = t.TaskDescription,
        HoursWorked     = t.HoursWorked,
        Status          = t.Status,
        ManagerRemarks  = t.ManagerRemarks,
        ApprovedByUserId= t.ApprovedByUserId,
        ApprovedAt      = t.ApprovedAt,
        CreatedAt       = t.CreatedAt,
        UpdatedAt       = t.UpdatedAt
    };
}
