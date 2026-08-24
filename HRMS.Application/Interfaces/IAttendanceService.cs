using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using Microsoft.AspNetCore.Http;

namespace HRMS.Application.Interfaces;

public interface IAttendanceService
{
    // ── Test / calculation API ────────────────────────────────────────────────

    /// <summary>
    /// Records a check-in for the given employee at the supplied timestamp.
    /// Idempotent: a second call on the same calendar day returns the existing
    /// attendance record ID without creating a duplicate row.
    /// </summary>
    /// <param name="employeeId">Domain employee code (e.g. "E001").</param>
    /// <param name="companyId">Tenant discriminator — enforces company isolation.</param>
    /// <param name="checkIn">Exact UTC timestamp of the check-in event.</param>
    /// <param name="ipAddress">Source IP for audit-log compliance.</param>
    /// <returns>Primary key of the created or existing <c>WebAttendance</c> row.</returns>
    Task<int> CheckInAsync(string employeeId, int companyId, DateTime checkIn, string ipAddress);

    /// <summary>
    /// Records a check-out at the supplied timestamp, recalculates attendance status,
    /// and stores any overtime minutes.  No-op when the record does not exist.
    /// </summary>
    Task CheckOutAsync(int attendanceId, DateTime checkOut);

    /// <summary>
    /// Returns all attendance records for the specified company scoped to the given
    /// date range.  Pass a non-null <paramref name="employeeId"/> to narrow to one person.
    /// </summary>
    Task<List<AttendanceDto>> GetAttendanceAsync(
        string?           employeeId,
        int               companyId,
        DateOnly          startDate,
        DateOnly          endDate,
        CancellationToken ct = default);

    /// <summary>
    /// Simple edit used by tests and lightweight admin flows.
    /// Sets <see cref="EditAttendanceDto.Status"/> and stores <see cref="EditAttendanceDto.Reason"/>
    /// in the <c>AdminEditReason</c> column.  Enforces tenant isolation.
    /// Returns <c>false</c> when the record does not exist or belongs to a different company.
    /// </summary>
    Task<bool> EditAttendanceAsync(EditAttendanceDto editDto, int actorId, int companyId);

    // ── Web / HTTP API ────────────────────────────────────────────────────────

    Task<int>  WebCheckInAsync(string employeeId);

    /// <summary>
    /// Records a check-out for the given attendance record.
    /// Pass <paramref name="ownerEmployeeId"/> (from the JWT claim) when called from an
    /// employee-facing endpoint so the service can enforce IDOR ownership: an employee who
    /// guesses another record's ID will receive <c>false</c> (→ 404) rather than checking
    /// out a colleague's attendance. Omit (or pass <c>null</c>) for admin callers, which
    /// route through <see cref="EditWebAttendanceAsync"/> instead.
    /// </summary>
    Task<bool> WebCheckOutAsync(int attendanceId, string? ownerEmployeeId = null);

    /// <summary>
    /// HR/Admin audited edit of any attendance record.
    /// Enforces back-dated edit window for non-admin callers.
    /// Applies PayrollLock check for the affected period.
    /// Logs all changes to AuditLog with the supplied reason.
    /// </summary>
    Task<(bool success, string message)> EditWebAttendanceAsync(
        int    attendanceId,
        string status,
        string reason,
        int    actorUserId,
        int    actorCompanyId,
        bool   isPrivilegedUser);

    Task<List<WebAttendanceDto>> GetWebAttendanceAsync(AttendanceFilterDto filter);
    // FIX 6: CancellationToken added — propagates HttpContext.RequestAborted through to DB queries.
    Task<PagedResult<WebAttendanceDto>> GetWebAttendancePagedAsync(AttendanceFilterDto filter, int page, int pageSize, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default);

    /// <summary>
    /// Parses an Excel attendance file and persists valid rows.
    /// Returns an <see cref="ExcelUploadResult"/> with imported/skipped counts and
    /// per-row error messages so callers can surface partial-failure details to the user.
    /// </summary>
    Task<ExcelUploadResult> UploadExcelAttendanceAsync(IFormFile file, int? companyId);
    Task<List<ExcelAttendanceDto>> GetExcelAttendanceAsync(AttendanceFilterDto filter);
    // FIX 6: CancellationToken added.
    Task<PagedResult<ExcelAttendanceDto>> GetExcelAttendancePagedAsync(AttendanceFilterDto filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Soft-delete an attendance record.
    /// Employees may only delete their own same-day record.
    /// Admins may delete any record within their company tenant.
    /// All deletions are audited.
    /// </summary>
    Task<bool> SoftDeleteAttendanceAsync(int attendanceId, string callerEmployeeId, bool isAdmin, string reason);
}
