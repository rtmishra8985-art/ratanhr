using HRMS.Application.Common;
using HRMS.Application.DTOs.Travel;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Travel;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class TravelService : ITravelService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TravelService> _logger;

    public TravelService(ApplicationDbContext db, ILogger<TravelService> logger)
    {
        _db = db; _logger = logger;
    }

    // ── Queries ────────────────────────────────────────────────────────────

    public async Task<PagedResult<TravelDto>> GetAllAsync(
        int? companyId, int page, int pageSize, string? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var q = _db.TravelRequests
            .Include(t => t.Approvals)
            .Include(t => t.History)
            .Where(t => !t.IsDeleted)
            .Where(t => companyId == null || t.CompanyId == companyId)
            .Where(t => status == null || t.Status == status)
            .OrderByDescending(t => t.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<TravelDto>.Create(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<List<TravelDto>> GetMyRequestsAsync(string employeeId)
    {
        var list = await _db.TravelRequests
            .Include(t => t.Approvals)
            .Include(t => t.History)
            .Where(t => t.EmployeeId == employeeId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<TravelDto?> GetByIdAsync(int id, int? companyId)
    {
        var t = await _db.TravelRequests
            .Include(x => x.Approvals)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (t == null) return null;
        if (companyId.HasValue && t.CompanyId.HasValue && t.CompanyId != companyId) return null;
        return ToDto(t);
    }

    public async Task<TravelDashboardDto> GetDashboardAsync(int? companyId)
    {
        var q = _db.TravelRequests.Where(t => !t.IsDeleted && (companyId == null || t.CompanyId == companyId));
        var now = DateTime.UtcNow;
        var monthStart  = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sixMonthsAgo = now.AddMonths(-6);

        // FIX: Replace the full ToListAsync() + in-memory LINQ with individual DB-side
        // aggregate queries. The previous approach loaded every TravelRequest row into
        // memory before counting/summing, causing multi-MB allocations for large companies.
        var totalRequests    = await q.CountAsync();
        var pendingApproval  = await q.CountAsync(t => t.Status == "Submitted" || t.Status == "ManagerApproved" || t.Status == "HRApproved");
        var approved         = await q.CountAsync(t => t.Status == "FinanceApproved" || t.Status == "Completed");
        var rejected         = await q.CountAsync(t => t.Status == "Rejected");
        var completed        = await q.CountAsync(t => t.Status == "Completed");
        var totalCost        = await q.SumAsync(t => t.EstimatedCost);
        var monthCost        = await q.Where(t => t.CreatedAt >= monthStart).SumAsync(t => t.EstimatedCost);

        // Monthly trend: group by year+month in the database, format in memory (small result set)
        var monthlyRaw = await q
            .Where(t => t.CreatedAt >= sixMonthsAgo)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Cost = g.Sum(x => x.EstimatedCost) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthly = monthlyRaw.Select(g => new TravelMonthlyStatDto
        {
            Month         = $"{g.Year:D4}-{g.Month:D2}",
            Count         = g.Count,
            EstimatedCost = g.Cost
        }).ToList();

        // By travel type: small cardinality, safe to project in DB
        var byTypeRaw = await q
            .GroupBy(t => t.TravelType)
            .Select(g => new { TravelType = g.Key, Count = g.Count() })
            .ToListAsync();

        var byType = byTypeRaw.Select(g => new TravelTypeStatDto { TravelType = g.TravelType, Count = g.Count }).ToList();

        return new TravelDashboardDto
        {
            TotalRequests      = totalRequests,
            PendingApproval    = pendingApproval,
            Approved           = approved,
            Rejected           = rejected,
            Completed          = completed,
            TotalEstimatedCost = totalCost,
            CurrentMonthCost   = monthCost,
            MonthlyTrend       = monthly,
            ByType             = byType
        };
    }

    public async Task<PagedResult<TravelDto>> GetReportAsync(int? companyId, TravelReportFilterDto filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize is < 1 or > 500) filter.PageSize = 25;
        var q = _db.TravelRequests
            .Include(t => t.Approvals)
            .Include(t => t.History)
            .Where(t => !t.IsDeleted && (companyId == null || t.CompanyId == companyId))
            .Where(t => filter.Status == null || t.Status == filter.Status)
            .Where(t => filter.TravelType == null || t.TravelType == filter.TravelType)
            .Where(t => filter.EmployeeId == null || t.EmployeeId == filter.EmployeeId)
            .Where(t => filter.FromDate == null || t.StartDate >= filter.FromDate)
            .Where(t => filter.ToDate == null || t.EndDate <= filter.ToDate)
            .OrderByDescending(t => t.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return PagedResult<TravelDto>.Create(items.Select(ToDto).ToList(), total, filter.Page, filter.PageSize);
    }

    // ── Employee actions ───────────────────────────────────────────────────

    public async Task<TravelDto> CreateAsync(string employeeId, int? companyId, CreateTravelDto dto)
    {
        var req = new TravelRequest
        {
            EmployeeId     = employeeId,
            CompanyId      = companyId,
            TravelType     = dto.TravelType,
            Purpose        = dto.Purpose,
            FromCity       = dto.FromCity,
            ToCity         = dto.ToCity,
            StartDate      = dto.StartDate,
            EndDate        = dto.EndDate,
            ModeOfTravel   = dto.ModeOfTravel,
            AdvanceRequired = dto.AdvanceRequired,
            AdvanceAmount  = dto.AdvanceAmount,
            EstimatedCost  = dto.EstimatedCost,
            Status         = "Draft",
            Notes          = dto.Notes,
            AttachmentPath = dto.AttachmentPath,
            CreatedBy      = employeeId,
            CreatedAt      = DateTime.UtcNow
        };
        _db.TravelRequests.Add(req);
        await _db.SaveChangesAsync();

        _db.TravelHistories.Add(new TravelHistory
        {
            TravelRequestId  = req.Id,
            CompanyId        = companyId,
            Action           = "Created",
            PreviousStatus   = null,
            NewStatus        = "Draft",
            PerformedBy      = employeeId,
            PerformedByName  = employeeId,
            CreatedAt        = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ToDto(req);
    }

    public async Task<TravelDto?> UpdateAsync(int id, string employeeId, int? companyId, UpdateTravelDto dto)
    {
        var req = await _db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null || req.EmployeeId != employeeId || req.IsDeleted) return null;
        if (req.Status != "Draft") return null;

        req.TravelType      = dto.TravelType;
        req.Purpose         = dto.Purpose;
        req.FromCity        = dto.FromCity;
        req.ToCity          = dto.ToCity;
        req.StartDate       = dto.StartDate;
        req.EndDate         = dto.EndDate;
        req.ModeOfTravel    = dto.ModeOfTravel;
        req.AdvanceRequired = dto.AdvanceRequired;
        req.AdvanceAmount   = dto.AdvanceAmount;
        req.EstimatedCost   = dto.EstimatedCost;
        req.Notes           = dto.Notes;
        req.AttachmentPath  = dto.AttachmentPath;
        req.UpdatedBy       = employeeId;
        req.UpdatedAt       = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(req);
    }

    public async Task<bool> SubmitAsync(int id, string employeeId)
    {
        var req = await _db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null || req.EmployeeId != employeeId || req.IsDeleted) return false;
        if (req.Status != "Draft") return false;

        var prev = req.Status;
        req.Status    = "Submitted";
        req.UpdatedAt = DateTime.UtcNow;

        _db.TravelApprovals.Add(new TravelApproval
        {
            TravelRequestId = id, CompanyId = req.CompanyId,
            Step = "Manager", Status = "Pending", CreatedAt = DateTime.UtcNow
        });
        _db.TravelHistories.Add(new TravelHistory
        {
            TravelRequestId = id, CompanyId = req.CompanyId,
            Action = "Submitted", PreviousStatus = prev, NewStatus = "Submitted",
            PerformedBy = employeeId, PerformedByName = employeeId, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelAsync(int id, string employeeId)
    {
        var req = await _db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null || req.EmployeeId != employeeId || req.IsDeleted) return false;
        if (req.Status is "FinanceApproved" or "Completed") return false;

        var prev = req.Status;
        req.Status    = "Cancelled";
        req.UpdatedAt = DateTime.UtcNow;
        _db.TravelHistories.Add(new TravelHistory
        {
            TravelRequestId = id, CompanyId = req.CompanyId,
            Action = "Cancelled", PreviousStatus = prev, NewStatus = "Cancelled",
            PerformedBy = employeeId, PerformedByName = employeeId, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, string employeeId)
    {
        var req = await _db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (req == null || req.EmployeeId != employeeId || req.IsDeleted) return false;
        if (req.Status != "Draft") return false;
        req.IsDeleted = true;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Approver actions ───────────────────────────────────────────────────

    public async Task<bool> DecideAsync(int id, int approverUserId, string approverName,
        int? companyId, TravelDecisionDto dto)
    {
        var req = await _db.TravelRequests
            .Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (req == null) return false;
        if (companyId.HasValue && req.CompanyId.HasValue && req.CompanyId != companyId) return false;

        // Find the pending approval for this step
        var approval = req.Approvals.FirstOrDefault(a => a.Step == dto.Step && a.Status == "Pending");

        var prevStatus = req.Status;

        // Direct decision: no multi-step approval chain exists for this request,
        // so the approver's verdict is final.
        if (approval == null)
        {
            if (req.Approvals.Any(a => a.Step == dto.Step && a.Status != "Pending"))
                return false; // this step was already decided

            if (dto.SendBack)
            {
                req.Status = "Draft";
            }
            else
            {
                req.Status     = dto.Approve ? "Approved" : "Rejected";
                req.ApprovedBy = approverUserId;
            }

            req.UpdatedAt = DateTime.UtcNow;
            _db.TravelHistories.Add(new TravelHistory
            {
                TravelRequestId = id, CompanyId = req.CompanyId,
                Action = dto.SendBack ? "Sent Back" : (dto.Approve ? "Approved" : "Rejected"),
                PreviousStatus = prevStatus, NewStatus = req.Status,
                PerformedBy = approverUserId.ToString(), PerformedByName = approverName,
                Remarks = dto.Comments, CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return true;
        }

        if (dto.SendBack)
        {
            approval.Status  = "SendBack";
            approval.ActionAt = DateTime.UtcNow;
            approval.ApproverId   = approverUserId;
            approval.ApproverName = approverName;
            approval.Comments = dto.Comments;
            req.Status = "Draft";
        }
        else if (dto.Approve)
        {
            approval.Status  = "Approved";
            approval.ActionAt = DateTime.UtcNow;
            approval.ApproverId   = approverUserId;
            approval.ApproverName = approverName;
            approval.Comments = dto.Comments;

            req.Status = dto.Step switch
            {
                "Manager" => "ManagerApproved",
                "HR"      => "HRApproved",
                "Finance" => "FinanceApproved",
                _         => req.Status
            };

            // Auto-create next approval step
            var nextStep = dto.Step switch
            {
                "Manager" => "HR",
                "HR"      => "Finance",
                _         => null
            };
            if (nextStep == null)
            {
                req.ApprovedBy = approverUserId;
            }
            else
            {
                _db.TravelApprovals.Add(new TravelApproval
                {
                    TravelRequestId = id, CompanyId = req.CompanyId,
                    Step = nextStep, Status = "Pending", CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            approval.Status  = "Rejected";
            approval.ActionAt = DateTime.UtcNow;
            approval.ApproverId   = approverUserId;
            approval.ApproverName = approverName;
            approval.Comments = dto.Comments;
            req.Status = "Rejected";
        }

        req.UpdatedAt = DateTime.UtcNow;
        _db.TravelHistories.Add(new TravelHistory
        {
            TravelRequestId = id, CompanyId = req.CompanyId,
            Action = dto.SendBack ? $"Sent Back by {dto.Step}" : (dto.Approve ? $"Approved by {dto.Step}" : $"Rejected by {dto.Step}"),
            PreviousStatus = prevStatus, NewStatus = req.Status,
            PerformedBy = approverUserId.ToString(), PerformedByName = approverName,
            Remarks = dto.Comments, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static TravelDto ToDto(TravelRequest t) => new()
    {
        Id             = t.Id,
        EmployeeId     = t.EmployeeId,
        TravelType     = t.TravelType,
        Purpose        = t.Purpose,
        FromCity       = t.FromCity,
        ToCity         = t.ToCity,
        StartDate      = t.StartDate,
        EndDate        = t.EndDate,
        ModeOfTravel   = t.ModeOfTravel,
        AdvanceRequired = t.AdvanceRequired,
        AdvanceAmount  = t.AdvanceAmount,
        EstimatedCost  = t.EstimatedCost,
        Status         = t.Status,
        Notes          = t.Notes,
        AttachmentPath = t.AttachmentPath,
        CreatedAt      = t.CreatedAt,
        UpdatedAt      = t.UpdatedAt,
        Approvals      = t.Approvals.Select(a => new TravelApprovalDto
        {
            Id = a.Id, Step = a.Step, Status = a.Status,
            ApproverName = a.ApproverName, Comments = a.Comments, ActionAt = a.ActionAt
        }).ToList(),
        History = t.History.OrderBy(h => h.CreatedAt).Select(h => new TravelHistoryDto
        {
            Id = h.Id, Action = h.Action, PreviousStatus = h.PreviousStatus,
            NewStatus = h.NewStatus, PerformedByName = h.PerformedByName,
            Remarks = h.Remarks, CreatedAt = h.CreatedAt
        }).ToList()
    };
}
