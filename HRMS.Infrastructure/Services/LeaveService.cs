using HRMS.Application.Common;
using HRMS.Application.DTOs.Leave;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public partial class LeaveService : ILeaveService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly ILogger<LeaveService> _logger;
    private readonly INotificationService _notify; // P4

    public LeaveService(ApplicationDbContext db, IAuditService audit,
                        IEmailService email, ILogger<LeaveService> logger,
                        INotificationService notify) // P4
    {
        _db = db; _audit = audit; _email = email; _logger = logger; _notify = notify;
    }

    // ── Leave Types ────────────────────────────────────────────────────────

    public async Task<List<LeaveTypeDto>> GetLeaveTypesAsync(int? companyId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var list = await _db.LeaveTypes
            .Where(t => t.IsActive && (t.CompanyId == null || t.CompanyId == companyId))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<PagedResult<LeaveTypeDto>> GetLeaveTypesPagedAsync(int? companyId, int page, int pageSize, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var q = _db.LeaveTypes
            .Where(t => t.IsActive && (t.CompanyId == null || t.CompanyId == companyId))
            .OrderBy(t => t.Name);
        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync(ct);
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedResult<LeaveTypeDto>.Create(rows.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(int? companyId, CreateLeaveTypeDto dto)
    {
        var lt = new LeaveType
        {
            Name = dto.Name, AnnualQuotaDays = dto.AnnualQuotaDays,
            IsPaid = dto.IsPaid, IsActive = true, CompanyId = companyId,
            CreatedAt = DateTime.UtcNow
        };
        _db.LeaveTypes.Add(lt);
        await _db.SaveChangesAsync();
        return ToDto(lt);
    }

    public async Task<bool> UpdateLeaveTypeAsync(int id, int? companyId, CreateLeaveTypeDto dto)
    {
        var lt = await _db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id);
        if (lt == null) return false;
        // Global leave types (CompanyId == null) are also off-limits to non-superadmin callers.
        if (companyId.HasValue && lt.CompanyId != companyId)
            return false;
        lt.Name = dto.Name; lt.AnnualQuotaDays = dto.AnnualQuotaDays; lt.IsPaid = dto.IsPaid;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteLeaveTypeAsync(int id, int? companyId)
    {
        var lt = await _db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id);
        if (lt == null) return false;
        if (companyId.HasValue && lt.CompanyId != companyId)
            return false;
        lt.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Employee self-service ─────────────────────────────────────────────

    public async Task<(bool ok, string message, int? id)> ApplyAsync(
        string employeeId, int? companyId, ApplyLeaveDto dto)
    {
        if (!DateOnly.TryParse(dto.StartDate, out var start) ||
            !DateOnly.TryParse(dto.EndDate,   out var end))
            return (false, "Invalid date format. Use yyyy-MM-dd.", null);

        if (end < start)
            return (false, "End date must be on or after start date.", null);

        var lt = await _db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == dto.LeaveTypeId);
        if (lt == null || !lt.IsActive) return (false, "Leave type not found or inactive.", null);

        // Overlap check
        var hasOverlap = await _db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId &&
            r.Status     != "Rejected"  &&
            r.Status     != "Cancelled" &&
            r.StartDate  <= end         &&
            r.EndDate    >= start);
        if (hasOverlap) return (false, "You have an overlapping leave request for those dates.", null);

        // Public holidays inside the range are not deducted from the employee's quota.
        var totalDays = await LeaveDaysAsync(companyId, start, end);
        if (totalDays <= 0)
            return (false, "The selected period contains no working days.", null);

        // Balance check
        var used    = await UsedDaysAsync(employeeId, dto.LeaveTypeId, start.Year);
        var credits = await AdjustmentNetDaysAsync(employeeId, dto.LeaveTypeId, start.Year);
        var remaining = lt.AnnualQuotaDays + credits - used;
        if (totalDays > remaining)
            return (false, $"Insufficient balance. Available: {remaining} day(s), Requested: {totalDays}.", null);

        var req = new LeaveRequest
        {
            EmployeeId  = employeeId,
            CompanyId   = companyId,
            LeaveTypeId = dto.LeaveTypeId,
            StartDate   = start,
            EndDate     = end,
            // TotalDays is only committed once the request is approved; while the
            // request is Pending the day count is computed on the fly so that a
            // rejection automatically restores the employee's balance.
            TotalDays   = 0,
            Reason      = dto.Reason,
            Status      = "Pending",
            CreatedAt   = DateTime.UtcNow
        };
        _db.LeaveRequests.Add(req);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("LEAVE_APPLY", "LeaveRequest", req.Id.ToString(), null, employeeId,
            details: $"{lt.Name}: {start} – {end} ({totalDays} day(s))");
        return (true, "Leave application submitted.", req.Id);
    }

    public async Task<List<LeaveRequestDto>> GetMyRequestsAsync(string employeeId)
    {
        var list = await _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return await EnrichRequestListAsync(list);
    }

    public async Task<List<LeaveBalanceDto>> GetMyBalanceAsync(string employeeId, int? companyId)
    {
        var types = await _db.LeaveTypes
            .Where(t => t.IsActive && (t.CompanyId == null || t.CompanyId == companyId))
            .ToListAsync();

        var result = new List<LeaveBalanceDto>();
        var year   = DateTime.UtcNow.Year;

        foreach (var lt in types)
        {
            var used    = await UsedDaysAsync(employeeId, lt.Id, year);
            var pending = await PendingDaysAsync(employeeId, lt.Id, year);
            var credits = await AdjustmentNetDaysAsync(employeeId, lt.Id, year);
            // FIX #1: UsedDaysAsync already counts Approved + Pending (everything
            // not Rejected/Cancelled). Subtracting 'pending' again double-counts and
            // shows employees an artificially low balance. Correct formula:
            // quota + adjustments - used. 'pending' is retained for UI display only.
            var remaining = Math.Max(0, lt.AnnualQuotaDays + credits - used);

            result.Add(new LeaveBalanceDto
            {
                LeaveTypeId    = lt.Id,
                LeaveTypeName  = lt.Name,
                AnnualQuotaDays = lt.AnnualQuotaDays,
                IsPaid         = lt.IsPaid,
                UsedDays       = used,
                PendingDays    = pending,
                RemainingDays  = remaining
            });
        }
        return result;
    }

    // IDOR FIX: added callerCompanyId — record is now scoped at DB level.
    // FindAsync was replaced with FirstOrDefaultAsync + WHERE so a request that
    // belongs to another tenant is never loaded into memory.
    public async Task<bool> CancelAsync(string employeeId, int requestId, int? callerCompanyId = null)
    {
        var q = _db.LeaveRequests.Where(r => r.Id == requestId && r.EmployeeId == employeeId);
        // Scope to caller's company at DB level when not superadmin
        if (callerCompanyId.HasValue)
            q = q.Where(r => r.CompanyId == callerCompanyId.Value);
        var req = await q.FirstOrDefaultAsync();
        if (req == null) return false;
        if (req.Status is not ("Pending" or "Approved")) return false;
        req.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("LEAVE_CANCEL", "LeaveRequest", req.Id.ToString(), null, employeeId);
        return true;
    }

    // ── Admin ──────────────────────────────────────────────────────────────

    // FIX HIGH-2: callerCompanyId is now part of the WHERE clause — the record is
    // never loaded if it belongs to a different tenant. This closes the IDOR at the
    // DB layer rather than relying on a post-fetch check in the controller.
    public async Task<LeaveRequestDto?> GetRequestByIdAsync(int id, int? callerCompanyId = null)
    {
        var q = _db.LeaveRequests.Where(r => r.Id == id);
        // Non-superadmin: scope to caller's company at DB level (IDOR fix)
        if (callerCompanyId.HasValue)
            q = q.Where(r => r.CompanyId == callerCompanyId.Value);
        var req = await q.FirstOrDefaultAsync();
        if (req == null) return null;
        var list = await EnrichRequestListAsync(new List<LeaveRequest> { req });
        return list.FirstOrDefault();
    }

    public async Task<List<LeaveRequestDto>> GetAllRequestsAsync(int? companyId, string? status)
    {
        var q = _db.LeaveRequests.AsQueryable();
        if (companyId.HasValue) q = q.Where(r => r.CompanyId == companyId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return await EnrichRequestListAsync(list);
    }

    // FIX 5: Added sortBy / sortDirection for column-level sorting support.
    public async Task<PagedResult<LeaveRequestDto>> GetAllRequestsPagedAsync(
        int?    companyId,
        string? status,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "desc")
    {
        var q = _db.LeaveRequests.AsQueryable();
        if (companyId.HasValue) q = q.Where(r => r.CompanyId == companyId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);

        // FIX 5: Safe sorting — whitelist prevents SQL injection.
        var allowed = new[] { "CreatedAt", "Status", "EmployeeId", "StartDate", "EndDate" };
        q = q.ApplySortingByDate(sortBy, sortDirection, r => r.CreatedAt, allowed);

        if (page < 1) page = 1; if (pageSize < 1) pageSize = 1; if (pageSize > 200) pageSize = 200;
        var total = await q.CountAsync();
        var list  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = await EnrichRequestListAsync(list);
        return PagedResult<LeaveRequestDto>.Create(items, total, page, pageSize);
    }

    // IDOR FIX: added callerCompanyId — record is now scoped at DB level.
    // FindAsync was replaced with FirstOrDefaultAsync + WHERE so an admin cannot
    // approve/reject a leave request belonging to a different tenant.
    public async Task<(bool ok, string message)> DecideAsync(
        int requestId, int approverUserId, LeaveDecisionDto dto, int? callerCompanyId = null)
    {
        var q = _db.LeaveRequests.Where(r => r.Id == requestId);
        // Scope to caller's company at DB level (non-superadmin only)
        if (callerCompanyId.HasValue)
            q = q.Where(r => r.CompanyId == callerCompanyId.Value);
        var req = await q.FirstOrDefaultAsync();
        if (req == null) return (false, "Leave request not found.");
        if (req.Status != "Pending") return (false, $"Request is already {req.Status}.");

        var approved    = dto.Approve;
        // Deduct on approval (holidays excluded), restore on rejection.
        req.TotalDays    = approved
            ? await LeaveDaysAsync(req.CompanyId, req.StartDate, req.EndDate)
            : 0;
        req.Status       = approved ? "Approved" : "Rejected";
        req.ApprovedByUserId = approverUserId;
        req.ApproverRemarks  = dto.Remarks;
        req.DecidedAt        = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            approved ? "LEAVE_APPROVE" : "LEAVE_REJECT",
            "LeaveRequest", req.Id.ToString(), approverUserId, null,
            details: $"Remarks: {dto.Remarks}");

        await SendDecisionEmailAsync(req, approved, dto.Remarks);

        // P4: Notify the employee about the leave decision
        try
        {
            var emp = await _db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeCode == req.EmployeeId);
            if (emp?.UserId != null)
            {
                var title   = approved ? "Leave Approved" : "Leave Rejected";
                var message = approved
                    ? $"Your leave request from {req.StartDate:MMM dd} to {req.EndDate:MMM dd} has been approved."
                    : $"Your leave request from {req.StartDate:MMM dd} to {req.EndDate:MMM dd} has been rejected. Remarks: {dto.Remarks ?? "None"}";
                // FIX HIGH-N3: Await so async exceptions are caught by the enclosing try/catch.
                await _notify.NotifyAsync(emp.UserId.Value, title, message,
                        approved ? "success" : "warning", "LeaveRequest", req.Id.ToString());
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "P4: Leave notification failed for request {Id}", req.Id); }

        return (true, approved ? "Leave approved." : "Leave rejected.");
    }

    // ── Balance Adjustment ─────────────────────────────────────────────────

    public async Task<LeaveBalanceAdjustmentDto> CreateBalanceAdjustmentAsync(
        int actorUserId, int? companyId, CreateLeaveBalanceAdjustmentDto dto)
    {
        var lt = await _db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == dto.LeaveTypeId)
            ?? throw new KeyNotFoundException("Leave type not found.");

        var adj = new LeaveBalanceAdjustment
        {
            EmployeeId        = dto.EmployeeId,
            CompanyId         = companyId,
            LeaveTypeId       = dto.LeaveTypeId,
            Year              = dto.Year,
            Days              = dto.Days,
            Reason            = dto.Reason,
            AdjustedByUserId  = actorUserId,
            CreatedAt         = DateTime.UtcNow
        };
        _db.LeaveBalanceAdjustments.Add(adj);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("LEAVE_BALANCE_ADJUST", "LeaveBalanceAdjustment", adj.Id.ToString(),
            actorUserId, null,
            details: $"Employee {dto.EmployeeId}, LeaveType {lt.Name}, {dto.Days:+#;-#;0} days, Year {dto.Year}. Reason: {dto.Reason}");

        return new LeaveBalanceAdjustmentDto
        {
            Id = adj.Id, EmployeeId = adj.EmployeeId, LeaveTypeId = adj.LeaveTypeId,
            LeaveTypeName = lt.Name, Year = adj.Year, Days = adj.Days,
            Reason = adj.Reason, AdjustedByUserId = adj.AdjustedByUserId, CreatedAt = adj.CreatedAt
        };
    }

    /// <summary>
    /// IDOR fix: validates the requested employee belongs to the caller's company
    /// when <paramref name="callerCompanyId"/> is not null (non-superadmin).
    /// Throws <see cref="UnauthorizedAccessException"/> on cross-company access.
    /// </summary>
    public async Task<List<LeaveBalanceAdjustmentDto>> GetBalanceAdjustmentsAsync(
        string employeeId, int? year, int? callerCompanyId = null)
    {
        // SECURITY FIX – LeaveController.GetAdjustments IDOR
        // Verify the target employee belongs to the caller's company before returning data.
        if (callerCompanyId.HasValue)
        {
            var emp = await _db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeCode == employeeId && e.CompanyId == callerCompanyId);
            if (emp == null)
                throw new UnauthorizedAccessException(
                    "Employee does not belong to your company.");
        }

        var q = _db.LeaveBalanceAdjustments.Where(a => a.EmployeeId == employeeId);
        if (year.HasValue) q = q.Where(a => a.Year == year.Value);
        var rows = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();

        var typeIds  = rows.Select(r => r.LeaveTypeId).Distinct().ToList();
        var typeDict = await _db.LeaveTypes.Where(t => typeIds.Contains(t.Id))
                                           .ToDictionaryAsync(t => t.Id, t => t.Name);

        return rows.Select(r => new LeaveBalanceAdjustmentDto
        {
            Id = r.Id, EmployeeId = r.EmployeeId, LeaveTypeId = r.LeaveTypeId,
            LeaveTypeName    = typeDict.GetValueOrDefault(r.LeaveTypeId, $"Type#{r.LeaveTypeId}"),
            Year = r.Year, Days = r.Days, Reason = r.Reason,
            AdjustedByUserId = r.AdjustedByUserId, CreatedAt = r.CreatedAt
        }).ToList();
    }

    // ── Carry Forward ──────────────────────────────────────────────────────

    public async Task<(int processed, int skipped)> CarryForwardBalancesAsync(
        LeaveCarryForwardDto dto, int actorUserId)
    {
        var types = await _db.LeaveTypes
            .Where(t => t.IsActive &&
                        (t.CompanyId == null || t.CompanyId == dto.CompanyId || dto.CompanyId == null))
            .ToListAsync();

        var empQ = _db.Employees.Where(e => e.IsActive);
        if (dto.CompanyId.HasValue) empQ = empQ.Where(e => e.CompanyId == dto.CompanyId);
        var employees = await empQ.Select(e => new { e.EmployeeCode, e.CompanyId }).ToListAsync();

        int processed = 0, skipped = 0;

        // N+1 FIX: Pre-load all carry-forward data in 2 bulk queries.
        // Previous pattern issued ApprovedOnlyDaysAsync() + AdjustmentNetDaysAsync() per
        // (employee × leave type) iteration — N×M×2 round-trips for N employees and M types.
        // Now: 2 queries total, regardless of dataset size.
        var empIds  = employees.Select(e => e.EmployeeCode).ToList();
        var typeIds = types.Select(t => t.Id).ToList();

        var approvedRaw = await _db.LeaveRequests
            .Where(r => empIds.Contains(r.EmployeeId)
                     && typeIds.Contains(r.LeaveTypeId)
                     && r.Status == "Approved"
                     && r.StartDate.Year == dto.FromYear)
            .GroupBy(r => new { r.EmployeeId, r.LeaveTypeId })
            .Select(g => new { g.Key.EmployeeId, g.Key.LeaveTypeId,
                               Days = g.Sum(r => (int?)r.TotalDays) ?? 0 })
            .ToListAsync();
        var approvedDays = approvedRaw
            .ToDictionary(x => (x.EmployeeId, x.LeaveTypeId), x => x.Days);

        var adjustRaw = await _db.LeaveBalanceAdjustments
            .Where(a => empIds.Contains(a.EmployeeId)
                     && typeIds.Contains(a.LeaveTypeId)
                     && a.Year == dto.FromYear)
            .GroupBy(a => new { a.EmployeeId, a.LeaveTypeId })
            .Select(g => new { g.Key.EmployeeId, g.Key.LeaveTypeId,
                               Days = g.Sum(a => (int?)a.Days) ?? 0 })
            .ToListAsync();
        var adjustDays = adjustRaw
            .ToDictionary(x => (x.EmployeeId, x.LeaveTypeId), x => x.Days);

        foreach (var emp in employees)
        {
            foreach (var lt in types)
            {
                // FIX #8: Carry-forward uses only APPROVED days (not pending) — correct.
                // N+1 FIX: now reads from pre-loaded dictionaries, no per-iteration DB calls.
                var used   = approvedDays.GetValueOrDefault((emp.EmployeeCode, lt.Id));
                var credit = adjustDays.GetValueOrDefault((emp.EmployeeCode, lt.Id));
                var remaining = lt.AnnualQuotaDays + credit - used;

                if (remaining <= 0) { skipped++; continue; }

                var carryDays = dto.MaxDays > 0 ? Math.Min(remaining, dto.MaxDays) : remaining;
                if (carryDays <= 0) { skipped++; continue; }

                _db.LeaveBalanceAdjustments.Add(new LeaveBalanceAdjustment
                {
                    EmployeeId       = emp.EmployeeCode,
                    CompanyId        = emp.CompanyId,
                    LeaveTypeId      = lt.Id,
                    Year             = dto.ToYear,
                    Days             = (int)carryDays,
                    Reason           = $"Carry forward from {dto.FromYear}",
                    AdjustedByUserId = actorUserId,
                    CreatedAt        = DateTime.UtcNow
                });
                processed++;
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("LEAVE_CARRY_FORWARD", "LeaveBalance", null, actorUserId, null,
            details: $"From {dto.FromYear} → {dto.ToYear}. Processed: {processed}, Skipped: {skipped}.");
        return (processed, skipped);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    // Counts Approved + Pending (i.e. all non-rejected, non-cancelled) days so that a pending
    // request still consumes quota and prevents double-booking while it awaits approval.
    private async Task<int> UsedDaysAsync(string employeeId, int leaveTypeId, int year)
        => await SumDaysAsync(_db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == leaveTypeId
                     && r.Status != "Rejected" && r.Status != "Cancelled"
                     && r.StartDate.Year == year));

    private async Task<int> PendingDaysAsync(string employeeId, int leaveTypeId, int year)
        => await SumDaysAsync(_db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == leaveTypeId
                     && r.Status == "Pending"
                     && r.StartDate.Year == year));

    // Approved requests carry a committed TotalDays; Pending ones are measured
    // from their date range (minus public holidays) so they still hold quota.
    private async Task<int> SumDaysAsync(IQueryable<LeaveRequest> query)
    {
        var rows = await query
            .Select(r => new { r.CompanyId, r.StartDate, r.EndDate, r.TotalDays })
            .ToListAsync();
        var total = 0;
        foreach (var r in rows)
            total += r.TotalDays > 0
                ? r.TotalDays
                : await LeaveDaysAsync(r.CompanyId, r.StartDate, r.EndDate);
        return total;
    }

    /// <summary>Calendar days in [start, end] minus active mandatory public holidays.</summary>
    private async Task<int> LeaveDaysAsync(int? companyId, DateOnly start, DateOnly end)
    {
        if (end < start) return 0;
        var calendarDays = (end.DayNumber - start.DayNumber) + 1;
        var holidays = await _db.HolidayCalendars.CountAsync(h =>
            h.IsActive && !h.IsOptional &&
            (h.CompanyId == null || h.CompanyId == companyId) &&
            h.Date >= start && h.Date <= end);
        return Math.Max(0, calendarDays - holidays);
    }

    // FIX #8 helper: counts only Approved days — used for carry-forward calculation
    // so that pending requests at year-end do not reduce an employee's carry-forward entitlement.
    private async Task<int> ApprovedOnlyDaysAsync(string employeeId, int leaveTypeId, int year)
        => await _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.LeaveTypeId == leaveTypeId
                     && r.Status == "Approved"
                     && r.StartDate.Year == year)
            .SumAsync(r => (int?)r.TotalDays) ?? 0;

    private async Task<int> AdjustmentNetDaysAsync(string employeeId, int leaveTypeId, int year)
        => await _db.LeaveBalanceAdjustments
            .Where(a => a.EmployeeId == employeeId && a.LeaveTypeId == leaveTypeId && a.Year == year)
            .SumAsync(a => (int?)a.Days) ?? 0;

    private async Task<List<LeaveRequestDto>> EnrichRequestListAsync(List<LeaveRequest> list)
    {
        var empIds  = list.Select(r => r.EmployeeId).Distinct().ToList();
        var typeIds = list.Select(r => r.LeaveTypeId).Distinct().ToList();

        var empNames  = await _db.Employees
            .Where(e => empIds.Contains(e.EmployeeCode))
            .ToDictionaryAsync(e => e.EmployeeCode, e => e.FullName);
        var typeNames = await _db.LeaveTypes
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        return list.Select(r => new LeaveRequestDto
        {
            Id             = r.Id,
            EmployeeId     = r.EmployeeId,
            EmployeeName   = empNames.GetValueOrDefault(r.EmployeeId),
            // Phase 1 – D: CompanyId exposed so controllers can IDOR-scope GetById
            CompanyId       = r.CompanyId,
            LeaveTypeId    = r.LeaveTypeId,
            LeaveTypeName  = typeNames.GetValueOrDefault(r.LeaveTypeId, $"Type#{r.LeaveTypeId}"),
            StartDate      = r.StartDate.ToString("yyyy-MM-dd"),
            EndDate        = r.EndDate.ToString("yyyy-MM-dd"),
            TotalDays      = r.TotalDays > 0
                                 ? r.TotalDays
                                 : (r.EndDate.DayNumber - r.StartDate.DayNumber) + 1,
            Reason         = r.Reason,
            Status         = r.Status,
            ApproverRemarks = r.ApproverRemarks,
            CreatedAt      = r.CreatedAt
        }).ToList();
    }

    private async Task SendDecisionEmailAsync(LeaveRequest req, bool approved, string? remarks)
    {
        try
        {
            var emp = await _db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeeCode == req.EmployeeId);
            if (emp == null) return;

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == emp.UserId);
            if (user?.Email == null) return;

            var lt = await _db.LeaveTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == req.LeaveTypeId);
            var leaveTypeName = lt?.Name ?? "Leave";

            await _email.SendLeaveDecisionAsync(
                user.Email,
                emp.FullName,
                leaveTypeName,
                req.StartDate.ToString("yyyy-MM-dd"),
                req.EndDate.ToString("yyyy-MM-dd"),
                approved,
                remarks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send leave decision email for request {Id}", req.Id);
        }
    }

    public async Task<List<LeaveRequestDto>> GetLeaveRequestsAsync(int? companyId, string? status = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await GetAllRequestsAsync(companyId, status);
    }

    private static LeaveTypeDto ToDto(LeaveType lt) => new()
    {
        Id = lt.Id, Name = lt.Name, AnnualQuotaDays = lt.AnnualQuotaDays,
        IsPaid = lt.IsPaid, IsActive = lt.IsActive, CompanyId = lt.CompanyId
    };
}
