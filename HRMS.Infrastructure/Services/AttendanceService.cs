using HRMS.Application.Common;
using HRMS.Application.DTOs.Attendance;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS.Infrastructure.Services;

public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext     _db;
    private readonly IAuditService            _audit;
    private readonly IPayrollLockGuard        _lockGuard;
    private readonly int                      _editWindowDays;
    private readonly ILogger<AttendanceService> _logger;
    private readonly IStreamingReportService? _streamingReport;

    // ── Production constructor ─────────────────────────────────────────────
    public AttendanceService(
        ApplicationDbContext     db,
        IAuditService            audit,
        IPayrollLockGuard        lockGuard,
        IConfiguration           config,
        ILogger<AttendanceService> logger,
        IStreamingReportService? streamingReport = null)
    {
        _db             = db;
        _audit          = audit;
        _lockGuard      = lockGuard;
        _logger          = logger;
        _streamingReport = streamingReport;
        // Default 7-day back-dated edit window; configurable via Attendance:BackDateEditWindowDays
        _editWindowDays = config.GetValue<int>("Attendance:BackDateEditWindowDays", 7);
    }

    /// <summary>
    /// Test-only constructor.  Accepts just the two mandatory collaborators and
    /// wires no-op implementations for everything else so unit tests do not need
    /// to build the full DI graph.
    /// </summary>
    internal AttendanceService(ApplicationDbContext db, IAuditService audit)
        : this(
            db,
            audit,
            new NopPayrollLockGuard(),
            new ConfigurationBuilder().Build(),
            NullLogger<AttendanceService>.Instance)
    { }

    // ── No-op PayrollLockGuard for unit tests ──────────────────────────────
    private sealed class NopPayrollLockGuard : IPayrollLockGuard
    {
        public Task<bool>    IsLockedAsync(int c, int m, int y)            => Task.FromResult(false);
        public Task<string?> GetLockMessageAsync(int c, int m, int y)      => Task.FromResult<string?>(null);
        public Task LockAsync(int c, int m, int y, int u, string? n)       => Task.CompletedTask;
        public Task UnlockAsync(int c, int m, int y, int u, string? n)     => Task.CompletedTask;
        public Task<List<PayrollLockDto>> GetLocksAsync(int? c, int? y)    => Task.FromResult(new List<PayrollLockDto>());
    }

    // ── Calculation API (used by unit tests and internal callers) ─────────

    /// <inheritdoc/>
    public async Task<int> CheckInAsync(
        string   employeeId,
        int      companyId,
        DateTime checkIn,
        string   ipAddress)
    {
        var today = DateOnly.FromDateTime(checkIn);

        // Idempotency: return the existing record when the employee already
        // checked in on the same calendar day within the same company.
        var existing = await _db.WebAttendances
            .FirstOrDefaultAsync(a =>
                a.EmployeeId == employeeId &&
                a.AttDate    == today &&
                (a.CompanyId == null || a.CompanyId == companyId));

        if (existing != null)
            return existing.Id;

        var attendance = new WebAttendance
        {
            EmployeeId = employeeId,
            CompanyId  = companyId,
            AttDate    = today,
            CheckIn    = TimeOnly.FromDateTime(checkIn),
            Status     = "Present"
        };
        _db.WebAttendances.Add(attendance);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            action:     "CHECKIN",
            entityType: "WebAttendance",
            entityId:   attendance.Id.ToString(),
            ipAddress:  ipAddress,
            details:    $"Employee {employeeId} checked in at {attendance.CheckIn:HH:mm:ss} UTC" +
                        $" on {today:yyyy-MM-dd}.");

        return attendance.Id;
    }

    /// <inheritdoc/>
    public async Task CheckOutAsync(int attendanceId, DateTime checkOut)
    {
        var att = await _db.WebAttendances.FirstOrDefaultAsync(x => x.Id == attendanceId);
        if (att == null) return;

        att.CheckOut = TimeOnly.FromDateTime(checkOut);

        double hours = 0;
        if (att.CheckIn.HasValue)
        {
            hours = (att.CheckOut.Value - att.CheckIn.Value).TotalHours;
            var shift = await GetEmployeeShiftAsync(att.EmployeeId);
            att.Status = CalculateAttendanceStatus(att.CheckIn.Value, att.CheckOut.Value, shift, hours);

            // Overtime: minutes beyond the standard 9-hour working day.
            const double standardHours = 9.0;
            att.OvertimeMinutes = hours > standardHours
                ? (int)Math.Round((hours - standardHours) * 60)
                : 0;
        }

        // BLOCKER-1 FIX: The stale catch block that referenced `existing`,
        // `employeeId`, and `today` (variables in WebCheckInAsync's scope)
        // was mistakenly left here during a previous refactor.  CheckOutAsync
        // updates a specific record by its PK (attendanceId) so a
        // duplicate-check-in unique-constraint race cannot occur here.
        // The SaveChangesAsync call is now unwrapped; any unexpected
        // DbUpdateException propagates to the caller as-is.
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            action:     "CHECKOUT",
            entityType: "WebAttendance",
            entityId:   att.Id.ToString(),
            details:    $"Employee {att.EmployeeId} checked out at {att.CheckOut:HH:mm:ss} UTC. " +
                        $"Status: {att.Status}. Hours: {hours:N2}. Overtime: {att.OvertimeMinutes} min.");
    }

    /// <inheritdoc/>
    public async Task<List<AttendanceDto>> GetAttendanceAsync(
        string?           employeeId,
        int               companyId,
        DateOnly          startDate,
        DateOnly          endDate,
        CancellationToken ct = default)
    {
        // Correlated subquery keeps company scoping in SQL — no client-side filtering.
        // Use EmployeeCode (string domain code) — EmployeeId is the [NotMapped] int PK alias.
        var empSubquery = _db.Employees
            .Where(e => e.CompanyId == companyId)
            .Select(e => e.EmployeeCode);

        var query = _db.WebAttendances
            .AsNoTracking()
            .Where(a =>
                empSubquery.Contains(a.EmployeeId) &&
                a.AttDate >= startDate &&
                a.AttDate <= endDate);

        if (!string.IsNullOrEmpty(employeeId))
            query = query.Where(a => a.EmployeeId == employeeId);

        var rows = await query.ToListAsync(ct);

        return rows.Select(a => new AttendanceDto
        {
            AttendanceId = a.Id,
            EmployeeId   = a.EmployeeId,
            CompanyId    = a.CompanyId ?? companyId,
            Date         = a.AttDate,
            CheckIn      = a.CheckIn,
            CheckOut     = a.CheckOut,
            Status       = a.Status,
            CreatedAt    = a.CreatedAt
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> EditAttendanceAsync(
        EditAttendanceDto editDto,
        int               actorId,
        int               companyId)
    {
        var att = await _db.WebAttendances.FirstOrDefaultAsync(x => x.Id == editDto.AttendanceId);
        if (att == null) return false;

        // Tenant isolation: verify the employee belongs to the acting company.
        // companyId == 0 is the superadmin bypass convention (no check required).
        // Use EmployeeCode (string domain code) — EmployeeId is the [NotMapped] int PK alias.
        if (companyId != 0)
        {
            var empExists = await _db.Employees
                .AnyAsync(e => e.EmployeeCode == att.EmployeeId && e.CompanyId == companyId);
            if (!empExists) return false;
        }

        att.Status          = editDto.Status;
        att.AdminEditReason = editDto.Reason;   // also visible via the Reason alias

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            action:     "AttendanceEdit",
            entityType: "WebAttendance",
            entityId:   editDto.AttendanceId.ToString(),
            actorId:    actorId,
            details:    $"Status set to '{editDto.Status}'. Employee: {att.EmployeeId}. " +
                        $"Date: {att.AttDate:yyyy-MM-dd}. Reason: {editDto.Reason}");

        return true;
    }

    // ── Check-in / Check-out ───────────────────────────────────────────────

    public async Task<int> WebCheckInAsync(string employeeId)
    {
        // FIX #7: DateTime.Today uses the server's local timezone clock; all other
        // timestamps in the codebase use UTC. On a UTC server with IST clients this
        // causes attendance records to be stamped with the wrong date after midnight IST.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.WebAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttDate == today);

        if (existing != null && existing.CheckIn.HasValue)
            return existing.Id; // Already checked in today — idempotent

        if (existing == null)
        {
            // FIX (RBAC/tenant-isolation bug): WebAttendance carries a global EF query
            // filter on CompanyId (CompanyId == _tenantCompanyId). This method previously
            // never set CompanyId, so every self-service check-in was created with
            // CompanyId = NULL. Because NULL never equals a tenant's CompanyId in SQL, the
            // row became permanently invisible to the checking-in employee's own
            // "my attendance" view, to their company's admin list/edit endpoints, and to
            // every other tenant-scoped query — it was reachable only by SuperAdmin
            // (whose queries bypass the filter). Resolve CompanyId from the Employee row
            // (EmployeeCode == employeeId) so the record is scoped correctly from creation.
            var companyId = await _db.Employees
                .Where(e => e.EmployeeCode == employeeId)
                .Select(e => (int?)e.CompanyId)
                .FirstOrDefaultAsync();

            existing = new WebAttendance
            {
                EmployeeId = employeeId,
                CompanyId  = companyId,
                AttDate    = today,
                CheckIn    = TimeOnly.FromDateTime(DateTime.UtcNow),
                Status     = "Present"
            };
            _db.WebAttendances.Add(existing);
        }
        else
        {
            existing.CheckIn = TimeOnly.FromDateTime(DateTime.UtcNow);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsAttendanceUniqueViolation(ex))
        {
            // Two requests can both pass the pre-check. The database unique
            // constraint is the final arbiter; return the winner's record.
            _db.Entry(existing).State = EntityState.Detached;
            return await _db.WebAttendances
                .Where(a => a.EmployeeId == employeeId && a.AttDate == today)
                .Select(a => a.Id)
                .FirstAsync();
        }

        // Audit log: record every check-in event for compliance and investigation trails
        await _audit.LogAsync(
            action:     "CHECKIN",
            entityType: "WebAttendance",
            entityId:   existing.Id.ToString(),
            details:    $"Employee {employeeId} checked in at {existing.CheckIn:HH:mm:ss} UTC on {today:yyyy-MM-dd}.");

        return existing.Id;
    }

    public async Task<bool> WebCheckOutAsync(int attendanceId, string? ownerEmployeeId = null)
    {
        var att = await _db.WebAttendances.FirstOrDefaultAsync(x => x.Id == attendanceId);
        if (att == null) return false;

        // IDOR: when called from an employee-facing endpoint the caller's employeeId is required.
        // Return false (→ 404) if the record belongs to a different employee so an attacker
        // cannot clock out a colleague by guessing sequential attendance IDs.
        if (ownerEmployeeId != null && att.EmployeeId != ownerEmployeeId)
            return false;

        att.CheckOut = TimeOnly.FromDateTime(DateTime.UtcNow);

        double hours = 0;
        if (att.CheckIn.HasValue)
        {
            hours = (att.CheckOut.Value - att.CheckIn.Value).TotalHours;

            // P5: Use shift-aware status if the employee has a shift assigned.
            // Falls back to the legacy 4h/8h thresholds when no shift is found.
            var shift = await GetEmployeeShiftAsync(att.EmployeeId);
            att.Status = CalculateAttendanceStatus(att.CheckIn.Value, att.CheckOut.Value, shift, hours);
        }

        await _db.SaveChangesAsync();

        // Audit log: record every check-out event including computed status
        await _audit.LogAsync(
            action:     "CHECKOUT",
            entityType: "WebAttendance",
            entityId:   att.Id.ToString(),
            details:    $"Employee {att.EmployeeId} checked out at {att.CheckOut:HH:mm:ss} UTC. " +
                        $"Status: {att.Status}. Hours worked: {hours:N2}.");

        return true;
    }

    private static bool IsAttendanceUniqueViolation(DbUpdateException ex)
    {
        var message = $"{ex.Message} {ex.InnerException?.Message}";
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    // ── P5: Shift-aware attendance helpers ─────────────────────────────────

    /// <summary>
    /// Loads the active shift assigned to the employee, or null if no shift is assigned.
    /// Results are not tracked to avoid contaminating the change tracker.
    /// </summary>
    private async Task<Shift?> GetEmployeeShiftAsync(string employeeId)
    {
        // FIX N+1: single JOIN instead of two sequential round-trips.
        // Previously: (1) load Employee to read ShiftId, (2) load Shift by id.
        // Now: one INNER JOIN in SQL — half the database round-trips on every checkout.
        // EmployeeCode is the string domain code ("E001"); Employee.EmployeeId is the
        // [NotMapped] int PK alias and cannot be used in EF queries.
        return await (
            from e in _db.Employees.AsNoTracking()
            where e.EmployeeCode == employeeId && e.ShiftId != null
            join s in _db.Shifts.AsNoTracking() on e.ShiftId equals s.Id
            where s.IsActive
            select s
        ).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Determines attendance status from check-in/out timestamps and an optional shift.
    ///
    /// With a shift:
    ///   Absent      — hours worked &lt; shift.HalfDayThresholdHours
    ///   Early Exit  — checked out more than EarlyExitThresholdMinutes before shift end
    ///   Half Day    — hours worked &lt; 75 % of the full shift duration
    ///   Late        — checked in after StartTime + GracePeriodMinutes + LateThresholdMinutes
    ///   Present     — on time, full shift, left on time
    ///
    /// Without a shift (legacy fallback):
    ///   Absent   — &lt; 4 hours
    ///   Half Day — 4–8 hours
    ///   Present  — ≥ 8 hours
    /// </summary>
    private static string CalculateAttendanceStatus(
        TimeOnly checkIn, TimeOnly checkOut, Shift? shift, double hoursWorked)
    {
        if (shift == null)
        {
            // Legacy fallback: kept for backward compatibility with unassigned-shift employees
            return hoursWorked >= 4 ? (hoursWorked >= 8 ? "Present" : "Half Day") : "Absent";
        }

        // Absent: did not meet minimum half-day threshold
        if (hoursWorked < (double)shift.HalfDayThresholdHours)
            return "Absent";

        // CheckIn/CheckOut are already TimeOnly — use directly for shift boundary comparisons.
        var checkInTime  = checkIn;
        var checkOutTime = checkOut;

        // Early Exit: left more than EarlyExitThresholdMinutes before shift end
        var earlyExitBefore = shift.EndTime.AddMinutes(-shift.EarlyExitThresholdMinutes);
        if (!shift.IsNightShift && checkOutTime < earlyExitBefore)
            return "Early Exit";

        // Full shift duration in hours
        double shiftHours = (shift.EndTime.ToTimeSpan() - shift.StartTime.ToTimeSpan()).TotalHours;
        if (shift.IsNightShift && shiftHours <= 0)
            shiftHours += 24; // handle midnight-crossing shifts

        // Half Day: worked less than 75 % of the full shift
        if (hoursWorked < shiftHours * 0.75)
            return "Half Day";

        // Late: checked in after grace + late threshold
        var lateAfter = shift.StartTime.AddMinutes(shift.GracePeriodMinutes + shift.LateThresholdMinutes);
        if (checkInTime > lateAfter)
            return "Late";

        return "Present";
    }

    // ── Audited admin edit with back-dated window + payroll-lock enforcement ──

    /// <inheritdoc/>
    public async Task<(bool success, string message)> EditWebAttendanceAsync(
        int    attendanceId,
        string status,
        string reason,
        int    actorUserId,
        int    actorCompanyId,
        bool   isPrivilegedUser)
    {
        var att = await _db.WebAttendances.FirstOrDefaultAsync(x => x.Id == attendanceId);
        if (att == null)
            return (false, "Attendance record not found.");

        // ── IDOR: verify attendance record's employee belongs to actor's company ──
        // actorCompanyId == 0 is the superadmin bypass — no IDOR check required.
        if (actorCompanyId != 0)
        {
            var empExists = await _db.Employees
                .AnyAsync(e => e.EmployeeCode == att.EmployeeId && e.CompanyId == actorCompanyId);
            if (!empExists)
                return (false, "Attendance record not found.");
        }

        // ── Back-dated window check ────────────────────────────────────────
        // FIX #7: Use UTC consistently (matches all other timestamps in the service).
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(-_editWindowDays);
        if (att.AttDate < cutoff && !isPrivilegedUser)
        {
            return (false,
                $"Cannot edit attendance older than {_editWindowDays} days. " +
                "Contact HR or an admin for corrections.");
        }

        // ── PayrollLock check ──────────────────────────────────────────────
        var lockMsg = await _lockGuard.GetLockMessageAsync(
            actorCompanyId, att.AttDate.Month, att.AttDate.Year);
        if (lockMsg != null)
            return (false, lockMsg);

        var previousStatus  = att.Status;
        att.Status          = status;
        att.AdminEditReason = reason;

        await _db.SaveChangesAsync();

        // ── Audit log ──────────────────────────────────────────────────────
        await _audit.LogAsync(
            action:     "AttendanceEdit",
            entityType: "WebAttendance",
            entityId:   attendanceId.ToString(),
            actorId:    actorUserId,
            details:    $"Status '{previousStatus}' → '{status}'. " +
                        $"Employee: {att.EmployeeId}. Date: {att.AttDate:yyyy-MM-dd}. " +
                        $"Reason: {reason}");

        return (true, "Attendance record updated.");
    }

    // ── Query ──────────────────────────────────────────────────────────────

    public async Task<List<WebAttendanceDto>> GetWebAttendanceAsync(AttendanceFilterDto filter)
    {
        var query = _db.WebAttendances.AsQueryable();

        if (!string.IsNullOrEmpty(filter.EmployeeId))
            query = query.Where(a => a.EmployeeId == filter.EmployeeId);
        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(a => a.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.StartDate))
        {
            if (!DateOnlyParser.TryParse(filter.StartDate).ok)
                throw new ArgumentException("Invalid StartDate format. Expected yyyy-MM-dd.");
            var start = DateOnlyParser.TryParse(filter.StartDate).date;
            query = query.Where(a => a.AttDate >= start);
        }
        if (!string.IsNullOrEmpty(filter.EndDate))
        {
            if (!DateOnlyParser.TryParse(filter.EndDate).ok)
                throw new ArgumentException("Invalid EndDate format. Expected yyyy-MM-dd.");
            var end = DateOnlyParser.TryParse(filter.EndDate).date;
            query = query.Where(a => a.AttDate <= end);
        }
        // FIX O2: use a correlated subquery so the company-scope filter is translated
        // entirely to SQL rather than loading all employee IDs into memory first.
        if (filter.CompanyId.HasValue)
        {
            var empSubquery = _db.Employees
                .Where(e => e.CompanyId == filter.CompanyId.Value)
                .Select(e => e.EmployeeCode);
            query = query.Where(a => empSubquery.Contains(a.EmployeeId));
        }

        // FIX O2: single LEFT JOIN for employee name eliminates the N+1 pattern where
        // records were loaded first, IDs collected, then a second query issued for names.
        // DateOnly.ToString() cannot be translated to SQL, so projection is done in memory
        // after a single SQL statement returns the joined rows.
        // Join on EmployeeCode (string domain code) — EmployeeId is the [NotMapped] int PK alias.
        var joined = await (
            from a in query.OrderByDescending(a => a.AttDate).Take(500)
            join e in _db.Employees on a.EmployeeId equals e.EmployeeCode into empJoin
            from emp in empJoin.DefaultIfEmpty()
            select new { Att = a, EmpName = (string?)emp.FullName })
            .ToListAsync();

        return joined.Select(x =>
        {
            var a = x.Att;
            TimeSpan? worked = a.CheckIn.HasValue && a.CheckOut.HasValue
                ? a.CheckOut.Value - a.CheckIn.Value : null;

            return new WebAttendanceDto
            {
                Id              = a.Id,
                EmployeeId      = a.EmployeeId,
                EmployeeName    = x.EmpName ?? a.EmployeeId,
                AttDate         = a.AttDate.ToString("yyyy-MM-dd"),
                CheckIn         = a.CheckIn?.ToString("HH:mm:ss", null),
                CheckOut        = a.CheckOut?.ToString("HH:mm:ss", null),
                Status          = a.Status,
                HoursWorked     = worked.HasValue ? Math.Round((decimal)worked.Value.TotalHours, 2) : null,
                AdminEditReason = a.AdminEditReason
            };
        }).ToList();
    }

    // ── Excel upload ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ExcelUploadResult> UploadExcelAttendanceAsync(IFormFile file, int? companyId)
    {
        // FIX: Return ExcelUploadResult instead of bool so the caller receives per-row
        // import/skip/error counts rather than a single success flag that hides partial failures.
        var result = new ExcelUploadResult();
        const int maxErrors = 50; // cap error list to avoid flooding large-file responses

        using var stream = file.OpenReadStream();

        // FIX 1: Stream rows via Open XML SDK SAX reader instead of ClosedXML XLWorkbook (OOM risk).
        // IStreamingReportService is always injected in production; null only in legacy unit tests.
        var rawRows = _streamingReport != null
            ? await _streamingReport.ReadAttendanceUploadRowsAsync(stream)
            : throw new InvalidOperationException(
                "IStreamingReportService is not registered. Ensure StreamingReportService is added to DI.");

        var rows = new List<ExcelAttendance>();
        foreach (var rawRow in rawRows)
        {
            var rowNum   = rawRow.RowNumber;
            var empId    = rawRow.EmployeeId;
            var dateStr  = rawRow.DateStr;
            var status   = rawRow.Status ?? "Present";
            var hoursStr = rawRow.HoursStr;

            if (string.IsNullOrEmpty(empId))
            {
                result.Skipped++;
                if (result.Errors.Count < maxErrors)
                    result.Errors.Add($"Row {rowNum}: EmployeeId is empty — skipped.");
                continue;
            }

            if (string.IsNullOrEmpty(dateStr))
            {
                result.Skipped++;
                if (result.Errors.Count < maxErrors)
                    result.Errors.Add($"Row {rowNum}: Date is empty — skipped.");
                continue;
            }

            if (!DateOnly.TryParse(dateStr, out var date))
            {
                result.Skipped++;
                if (result.Errors.Count < maxErrors)
                    result.Errors.Add($"Row {rowNum}: Cannot parse date '{dateStr}' (expected yyyy-MM-dd) — skipped.");
                continue;
            }

            decimal? hours = null;
            if (!string.IsNullOrEmpty(hoursStr) && !decimal.TryParse(hoursStr, out var h))
            {
                if (result.Errors.Count < maxErrors)
                    result.Errors.Add($"Row {rowNum}: HoursWorked '{hoursStr}' is not a valid decimal — defaulting to null.");
            }
            else if (!string.IsNullOrEmpty(hoursStr))
            {
                hours = decimal.Parse(hoursStr);
            }

            rows.Add(new ExcelAttendance
            {
                EmployeeId  = empId,
                AttDate     = date,
                Status      = status,
                HoursWorked = hours,
                CompanyId   = companyId
            });
            result.Imported++;
        }

        if (rows.Count > 0)
        {
            _db.ExcelAttendances.AddRange(rows);
            await _db.SaveChangesAsync();
        }

        return result;
    }

    public async Task<List<ExcelAttendanceDto>> GetExcelAttendanceAsync(AttendanceFilterDto filter)
    {
        var query = _db.ExcelAttendances.AsQueryable();

        if (!string.IsNullOrEmpty(filter.EmployeeId))
            query = query.Where(a => a.EmployeeId == filter.EmployeeId);
        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(a => a.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.StartDate))
        {
            if (!DateOnlyParser.TryParse(filter.StartDate).ok)
                throw new ArgumentException("Invalid StartDate format. Expected yyyy-MM-dd.");
            var start = DateOnlyParser.TryParse(filter.StartDate).date;
            query = query.Where(a => a.AttDate >= start);
        }
        if (!string.IsNullOrEmpty(filter.EndDate))
        {
            if (!DateOnlyParser.TryParse(filter.EndDate).ok)
                throw new ArgumentException("Invalid EndDate format. Expected yyyy-MM-dd.");
            var end = DateOnlyParser.TryParse(filter.EndDate).date;
            query = query.Where(a => a.AttDate <= end);
        }

        var records  = await query.OrderByDescending(a => a.AttDate).Take(500).ToListAsync();
        var empIds   = records.Select(r => r.EmployeeId).Distinct().ToList();
        var employees = await _db.Employees
            .Where(e => empIds.Contains(e.EmployeeCode))
            .ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);

        return records.Select(a => new ExcelAttendanceDto
        {
            Id           = a.Id,
            EmployeeId   = a.EmployeeId,
            EmployeeName = employees.GetValueOrDefault(a.EmployeeId, a.EmployeeId),
            AttDate      = a.AttDate.ToString("yyyy-MM-dd"),
            Status       = a.Status,
            HoursWorked  = a.HoursWorked
        }).ToList();
    }

    // FIX 6: CancellationToken propagated to CountAsync and ToListAsync.
    public async Task<PagedResult<WebAttendanceDto>> GetWebAttendancePagedAsync(AttendanceFilterDto filter, int page, int pageSize, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default)
    {
        // AsNoTracking: read-only paged query — no change tracking overhead.
        var query = _db.WebAttendances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(filter.EmployeeId))
            query = query.Where(a => a.EmployeeId == filter.EmployeeId);
        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(a => a.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.StartDate) && DateOnlyParser.TryParse(filter.StartDate).ok)
            query = query.Where(a => a.AttDate >= DateOnlyParser.TryParse(filter.StartDate).date);
        if (!string.IsNullOrEmpty(filter.EndDate) && DateOnlyParser.TryParse(filter.EndDate).ok)
            query = query.Where(a => a.AttDate <= DateOnlyParser.TryParse(filter.EndDate).date);
        if (filter.CompanyId.HasValue)
        {
            var compEmpIds = await _db.Employees.AsNoTracking().Where(e => e.CompanyId == filter.CompanyId.Value)
                .Select(e => e.EmployeeCode).ToListAsync();
            query = query.Where(a => compEmpIds.Contains(a.EmployeeId));
        }
        // FIX (production-complete): full SQL-level sorting for all documented columns.
        // EmployeeName resolved via correlated subquery (EF Core → SQL LEFT JOIN).
        // WorkingHours is computed from CheckIn/CheckOut and not stored; sorts by CheckIn as proxy.
        // Overtime and Shift are not stored on WebAttendance; fall back to AttDate desc.
        _logger.LogInformation(
            "GetWebAttendancePagedAsync requested: sortBy={SortBy} sortDirection={SortDirection} page={Page} pageSize={PageSize}",
            sortBy, sortDirection, page, pageSize);

        bool desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var effectiveSortBy = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;
        query = effectiveSortBy switch
        {
            "employeename" => desc
                ? query.OrderByDescending(a => _db.Employees
                    .Where(e => e.EmployeeCode == a.EmployeeId)
                    .Select(e => e.FullName)
                    .FirstOrDefault() ?? "")
                : query.OrderBy(a => _db.Employees
                    .Where(e => e.EmployeeCode == a.EmployeeId)
                    .Select(e => e.FullName)
                    .FirstOrDefault() ?? ""),
            // AttendanceDate maps to the AttDate stored column.
            "attendancedate" => desc ? query.OrderByDescending(a => a.AttDate)  : query.OrderBy(a => a.AttDate),
            "checkin"        => desc ? query.OrderByDescending(a => a.CheckIn)  : query.OrderBy(a => a.CheckIn),
            "checkout"       => desc ? query.OrderByDescending(a => a.CheckOut) : query.OrderBy(a => a.CheckOut),
            "status"         => desc ? query.OrderByDescending(a => a.Status)   : query.OrderBy(a => a.Status),
            // WorkingHours is computed (CheckOut - CheckIn) and not persisted; use CheckIn as SQL-sortable proxy.
            "workinghours"   => desc ? query.OrderByDescending(a => a.CheckIn)  : query.OrderBy(a => a.CheckIn),
            // Overtime and Shift are not stored columns on WebAttendance — default fallback.
            _                => query.OrderByDescending(a => a.AttDate)
        };

        _logger.LogInformation(
            "Attendance sort applied: effectiveSortBy={EffectiveSortBy} desc={Desc}",
            effectiveSortBy, desc);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total   = await query.CountAsync(ct);
        var records = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var empIds  = records.Select(r => r.EmployeeId).Distinct().ToList();
        var emps    = await _db.Employees.Where(e => empIds.Contains(e.EmployeeCode)).ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName, ct);
        var items   = records.Select(a => {
            TimeSpan? worked = a.CheckIn.HasValue && a.CheckOut.HasValue ? a.CheckOut.Value - a.CheckIn.Value : null;
            return new WebAttendanceDto { Id = a.Id, EmployeeId = a.EmployeeId,
                EmployeeName = emps.GetValueOrDefault(a.EmployeeId) ?? string.Empty,
                AttDate = a.AttDate.ToString("yyyy-MM-dd"), Status = a.Status,
                CheckIn = a.CheckIn?.ToString("HH:mm", null), CheckOut = a.CheckOut?.ToString("HH:mm", null),
                HoursWorked = worked.HasValue ? (decimal?)Math.Round(worked.Value.TotalHours, 2) : null };
        }).ToList();
        // Echo sortBy/sortDirection so callers can confirm the applied sort.
        return PagedResult<WebAttendanceDto>.Create(items, total, page, pageSize,
            sortBy: string.IsNullOrEmpty(effectiveSortBy) ? null : effectiveSortBy,
            sortDirection: desc ? "desc" : "asc");
    }

    // ── Soft Delete (FIX CRITICAL-4) ──────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> SoftDeleteAttendanceAsync(int attendanceId, string callerEmployeeId, bool isAdmin, string reason)
    {
        var att = await _db.WebAttendances
            .FirstOrDefaultAsync(a => a.Id == attendanceId && !a.IsDeleted);

        if (att == null) return false;

        if (!isAdmin)
        {
            // Employees may only delete their own same-day record
            if (att.EmployeeId != callerEmployeeId) return false;
            if (att.AttDate != DateOnly.FromDateTime(DateTime.UtcNow)) return false;
        }
        // Admins: company isolation is enforced by the EF global query filter on WebAttendance.CompanyId
        // so att would be null if it belongs to a different tenant.

        // ── Payroll lock guard (FIX COMPLIANCE-1) ─────────────────────────
        // Block deletion when the attendance record falls inside a locked payroll
        // period. Superadmins (companyId == 0 bypass convention) are still blocked
        // here; a deliberate unlock must happen first via PayrollController.
        var lockMsg = await _lockGuard.GetLockMessageAsync(
            att.CompanyId ?? 0, att.AttDate.Month, att.AttDate.Year);
        if (lockMsg is not null)
        {
            _logger.LogWarning(
                "SoftDeleteAttendance blocked — payroll period locked. AttendanceId={Id}, Period={Month}/{Year}, Reason={Lock}",
                attendanceId, att.AttDate.Month, att.AttDate.Year, lockMsg);
            throw new InvalidOperationException(lockMsg);
        }

        att.IsDeleted     = true;
        att.DeletedAt     = DateTime.UtcNow;
        att.DeletedReason = reason;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            action:     "DELETE",
            entityType: "WebAttendance",
            entityId:   att.Id.ToString(),
            details:    $"Attendance for employee {att.EmployeeId} on {att.AttDate:yyyy-MM-dd} soft-deleted by {(isAdmin ? "admin" : "employee")} {callerEmployeeId}. Reason: {reason}");

        _logger.LogInformation(
            "WebAttendance {Id} soft-deleted by {Caller} (admin={IsAdmin}). Reason: {Reason}",
            att.Id, callerEmployeeId, isAdmin, reason);

        return true;
    }

    // FIX 6: CancellationToken propagated to CountAsync and ToListAsync.
    public async Task<PagedResult<ExcelAttendanceDto>> GetExcelAttendancePagedAsync(AttendanceFilterDto filter, int page, int pageSize, CancellationToken ct = default)
    {
        // AsNoTracking: read-only paged query — no change tracking overhead.
        var query = _db.ExcelAttendances.AsNoTracking().AsQueryable();
        if (filter.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == filter.CompanyId.Value);
        if (!string.IsNullOrEmpty(filter.EmployeeId)) query = query.Where(a => a.EmployeeId == filter.EmployeeId);
        if (!string.IsNullOrEmpty(filter.Status))     query = query.Where(a => a.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.StartDate) && DateOnlyParser.TryParse(filter.StartDate).ok)
            query = query.Where(a => a.AttDate >= DateOnlyParser.TryParse(filter.StartDate).date);
        if (!string.IsNullOrEmpty(filter.EndDate) && DateOnlyParser.TryParse(filter.EndDate).ok)
            query = query.Where(a => a.AttDate <= DateOnlyParser.TryParse(filter.EndDate).date);
        query = query.OrderByDescending(a => a.AttDate);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total   = await query.CountAsync(ct);
        var records = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var empIds  = records.Select(r => r.EmployeeId).Distinct().ToList();
        var emps    = await _db.Employees.Where(e => empIds.Contains(e.EmployeeCode)).ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName, ct);
        var items   = records.Select(a => new ExcelAttendanceDto { Id = a.Id, EmployeeId = a.EmployeeId,
            EmployeeName = emps.GetValueOrDefault(a.EmployeeId) ?? string.Empty,
            AttDate = a.AttDate.ToString("yyyy-MM-dd"), Status = a.Status,
            HoursWorked = a.HoursWorked }).ToList();
        return PagedResult<ExcelAttendanceDto>.Create(items, total, page, pageSize);
    }
}
