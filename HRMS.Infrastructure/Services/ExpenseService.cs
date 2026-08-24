using HRMS.Application.Common;
using HRMS.Application.DTOs.Expense;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Expense;
using HRMS.Infrastructure.Data;
using HRMS.Infrastructure.FileStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using HRMS.Infrastructure.Security;

namespace HRMS.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _db;
    private readonly FileStorageService _fileStorage;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(ApplicationDbContext db, FileStorageService fileStorage,
                          ILogger<ExpenseService> logger)
    {
        _db = db; _fileStorage = fileStorage; _logger = logger;
    }

    // ── Queries ────────────────────────────────────────────────────────────

    public async Task<PagedResult<ExpenseDto>> GetAllAsync(
        int? companyId, int page, int pageSize, string? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;
        var q = _db.ExpenseClaims
            .Include(e => e.Items)
            .Include(e => e.Attachments)
            .Include(e => e.Approvals)
            .Include(e => e.History)
            .Where(e => !e.IsDeleted && (companyId == null || e.CompanyId == companyId))
            .Where(e => status == null || e.Status == status)
            .OrderByDescending(e => e.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<ExpenseDto>.Create(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<List<ExpenseDto>> GetMyClaimsAsync(string employeeId)
    {
        var list = await _db.ExpenseClaims
            .Include(e => e.Items)
            .Include(e => e.Attachments)
            .Include(e => e.Approvals)
            .Include(e => e.History)
            .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id, int? companyId)
    {
        var e = await _db.ExpenseClaims
            .Include(x => x.Items)
            .Include(x => x.Attachments)
            .Include(x => x.Approvals)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (e == null) return null;
        if (companyId.HasValue && e.CompanyId.HasValue && e.CompanyId != companyId) return null;
        return ToDto(e);
    }

    public async Task<ExpenseDto?> GetMyClaimByIdAsync(string employeeId, int id)
    {
        var e = await _db.ExpenseClaims
            .Include(x => x.Items)
            .Include(x => x.Attachments)
            .Include(x => x.Approvals)
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId && !x.IsDeleted);
        return e == null ? null : ToDto(e);
    }

    public async Task<ExpenseDashboardDto> GetDashboardAsync(int? companyId)
    {
        // FIX: Remove .Include(e => e.Items) — the dashboard query loaded every child row
        // for every claim in the company, causing huge N×M memory spikes. Aggregate sums
        // are pushed to the database instead.
        var q = _db.ExpenseClaims
            .Where(e => !e.IsDeleted && (companyId == null || e.CompanyId == companyId));

        var now         = DateTime.UtcNow;
        var monthStart  = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sixMonthsAgo = now.AddMonths(-6);

        var totalClaims    = await q.CountAsync();
        var pending        = await q.CountAsync(e => e.Status == "Submitted" || e.Status == "ManagerApproved");
        var approved       = await q.CountAsync(e => e.Status == "FinanceApproved");
        var rejected       = await q.CountAsync(e => e.Status == "Rejected");
        var approvedAmount = await q.Where(e => e.Status == "FinanceApproved").SumAsync(e => e.TotalAmount);
        var monthAmount    = await q.Where(e => e.CreatedAt >= monthStart).SumAsync(e => e.TotalAmount);

        // Monthly trend — group in DB, format strings in memory (small result set)
        var monthlyRaw = await q
            .Where(e => e.CreatedAt >= sixMonthsAgo)
            .GroupBy(e => new { e.CreatedAt.Year, e.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(e => e.TotalAmount), Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var monthly = monthlyRaw.Select(g => new ExpenseMonthlyStatDto
        {
            Month       = $"{g.Year:D4}-{g.Month:D2}",
            TotalAmount = g.Amount,
            ClaimCount  = g.Count
        }).ToList();

        // By category — aggregate ExpenseItems in DB, no claim header needed
        var byCategoryRaw = await _db.ExpenseItems
            .Where(i => !i.ExpenseClaim!.IsDeleted && (companyId == null || i.CompanyId == companyId))
            .GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(i => i.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        var byCategory = byCategoryRaw.Select(g => new ExpenseCategoryStatDto
        {
            Category    = g.Category,
            TotalAmount = g.Total,
            ItemCount   = g.Count
        }).ToList();

        return new ExpenseDashboardDto
        {
            TotalClaims         = totalClaims,
            PendingApproval     = pending,
            Approved            = approved,
            Rejected            = rejected,
            TotalApprovedAmount = approvedAmount,
            CurrentMonthAmount  = monthAmount,
            MonthlyTrend        = monthly,
            ByCategory          = byCategory
        };
    }

    public async Task<PagedResult<ExpenseDto>> GetReportAsync(int? companyId, ExpenseReportFilterDto filter)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize is < 1 or > 500) filter.PageSize = 25;
        var q = _db.ExpenseClaims
            .Include(e => e.Items)
            .Include(e => e.Attachments)
            .Include(e => e.Approvals)
            .Include(e => e.History)
            .Where(e => !e.IsDeleted && (companyId == null || e.CompanyId == companyId))
            .Where(e => filter.Status == null || e.Status == filter.Status)
            .Where(e => filter.EmployeeId == null || e.EmployeeId == filter.EmployeeId)
            .Where(e => filter.FromDate == null || e.CreatedAt >= filter.FromDate)
            .Where(e => filter.ToDate == null || e.CreatedAt <= filter.ToDate)
            .OrderByDescending(e => e.CreatedAt);
        var total = await q.CountAsync();
        var items = await q.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return PagedResult<ExpenseDto>.Create(items.Select(ToDto).ToList(), total, filter.Page, filter.PageSize);
    }

    // ── Employee actions ───────────────────────────────────────────────────

    public async Task<ExpenseDto> CreateDraftAsync(string employeeId, int? companyId, CreateExpenseClaimDto dto)
    {
        var claim = new ExpenseClaim
        {
            EmployeeId      = employeeId,
            CompanyId       = companyId,
            Title           = dto.Title,
            Currency        = dto.Currency,
            TravelRequestId = dto.TravelRequestId,
            Notes           = dto.Notes,
            Status          = "Draft",
            CreatedBy       = employeeId,
            CreatedAt       = DateTime.UtcNow
        };
        _db.ExpenseClaims.Add(claim);
        await _db.SaveChangesAsync();

        // Add line items
        foreach (var itemDto in dto.Items)
        {
            string? receiptPath = null;
            if (itemDto.Receipt != null)
                receiptPath = await _fileStorage.SaveAsync(itemDto.Receipt, "expense-items", UploadProfile.Document);

            claim.Items.Add(new ExpenseItem
            {
                ExpenseClaimId = claim.Id,
                CompanyId      = companyId,
                Category       = itemDto.Category,
                Description    = itemDto.Description,
                Amount         = itemDto.Amount,
                GstAmount      = itemDto.GstAmount,
                Currency       = itemDto.Currency,
                ExpenseDate    = itemDto.ExpenseDate,
                ReceiptPath    = receiptPath,
                CreatedAt      = DateTime.UtcNow
            });
        }

        claim.TotalAmount = claim.Items.Sum(i => i.Amount);
        claim.TotalGst    = claim.Items.Sum(i => i.GstAmount);

        _db.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseClaimId = claim.Id, CompanyId = companyId,
            Action = "Created", NewStatus = "Draft",
            PerformedBy = employeeId, PerformedByName = employeeId, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ToDto(claim);
    }

    public async Task<bool> SubmitAsync(int id, string employeeId)
    {
        var claim = await _db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == id);
        if (claim == null || claim.EmployeeId != employeeId || claim.IsDeleted) return false;
        if (claim.Status != "Draft") return false;

        var prev = claim.Status;
        claim.Status      = "Submitted";
        claim.SubmittedAt = DateTime.UtcNow;
        claim.UpdatedAt   = DateTime.UtcNow;

        _db.ExpenseApprovals.Add(new ExpenseApproval
        {
            ExpenseClaimId = id, CompanyId = claim.CompanyId,
            Step = "Manager", Status = "Pending", CreatedAt = DateTime.UtcNow
        });
        _db.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseClaimId = id, CompanyId = claim.CompanyId,
            Action = "Submitted", PreviousStatus = prev, NewStatus = "Submitted",
            PerformedBy = employeeId, PerformedByName = employeeId, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, string employeeId)
    {
        var claim = await _db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == id);
        if (claim == null || claim.EmployeeId != employeeId || claim.IsDeleted) return false;
        if (claim.Status != "Draft") return false;
        claim.IsDeleted = true;
        claim.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Approver actions ───────────────────────────────────────────────────

    public async Task<bool> DecideAsync(int id, int reviewerUserId, string reviewerName,
        int? companyId, ExpenseDecisionDto dto)
    {
        var claim = await _db.ExpenseClaims
            .Include(e => e.Approvals)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (claim == null) return false;
        if (companyId.HasValue && claim.CompanyId.HasValue && claim.CompanyId != companyId) return false;

        var approval = claim.Approvals.FirstOrDefault(a => a.Step == dto.Step && a.Status == "Pending");
        if (approval == null) return false;

        var prevStatus = claim.Status;

        if (dto.SendBack)
        {
            approval.Status = "SendBack";
            approval.ActionAt = DateTime.UtcNow;
            approval.ApproverId   = reviewerUserId;
            approval.ApproverName = reviewerName;
            approval.Comments = dto.Comments;
            claim.Status = "Draft";
        }
        else if (dto.Approve)
        {
            approval.Status = "Approved";
            approval.ActionAt = DateTime.UtcNow;
            approval.ApproverId   = reviewerUserId;
            approval.ApproverName = reviewerName;
            approval.Comments = dto.Comments;

            claim.Status = dto.Step switch
            {
                "Manager" => "ManagerApproved",
                "Finance" => "FinanceApproved",
                _         => claim.Status
            };
            if (dto.Step == "Manager")
            {
                _db.ExpenseApprovals.Add(new ExpenseApproval
                {
                    ExpenseClaimId = id, CompanyId = claim.CompanyId,
                    Step = "Finance", Status = "Pending", CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            approval.Status = "Rejected";
            approval.ActionAt = DateTime.UtcNow;
            approval.ApproverId   = reviewerUserId;
            approval.ApproverName = reviewerName;
            approval.Comments = dto.Comments;
            claim.Status = "Rejected";
        }

        claim.UpdatedAt = DateTime.UtcNow;
        _db.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseClaimId = id, CompanyId = claim.CompanyId,
            Action = dto.SendBack ? $"Sent Back by {dto.Step}" : (dto.Approve ? $"Approved by {dto.Step}" : $"Rejected by {dto.Step}"),
            PreviousStatus = prevStatus, NewStatus = claim.Status,
            PerformedBy = reviewerUserId.ToString(), PerformedByName = reviewerName,
            Remarks = dto.Comments, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Legacy shims ───────────────────────────────────────────────────────

#pragma warning disable CS0618
    public async Task<ExpenseDto> SubmitLegacyAsync(string employeeId, int? companyId, CreateExpenseDto dto)
    {
        string? receiptPath = null;
        if (dto.Receipt != null)
            receiptPath = await _fileStorage.SaveAsync(dto.Receipt, "expenses", UploadProfile.Document);

        var claim = new ExpenseClaim
        {
            EmployeeId  = employeeId,
            CompanyId   = companyId,
            Title       = dto.Title,
            Currency    = dto.Currency ?? "INR",
            Notes       = dto.Notes,
            Status      = "Submitted",
            SubmittedAt = DateTime.UtcNow,
            CreatedBy   = employeeId,
            CreatedAt   = DateTime.UtcNow
        };
        claim.Items.Add(new ExpenseItem
        {
            CompanyId   = companyId,
            Category    = dto.Category ?? "Miscellaneous",
            Description = dto.Title,
            Amount      = dto.Amount,
            Currency    = dto.Currency ?? "INR",
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReceiptPath = receiptPath,
            CreatedAt   = DateTime.UtcNow
        });
        claim.TotalAmount = dto.Amount;
        _db.ExpenseClaims.Add(claim);
        await _db.SaveChangesAsync();
        return ToDto(claim);
    }

    public async Task<bool> DecideLegacyAsync(int id, int reviewerUserId, int? companyId, ExpenseDecisionDto dto)
        => await DecideAsync(id, reviewerUserId, reviewerUserId.ToString(), companyId, dto);
#pragma warning restore CS0618

    // ── Mapping ────────────────────────────────────────────────────────────

    private static ExpenseDto ToDto(ExpenseClaim e) => new()
    {
        Id              = e.Id,
        EmployeeId      = e.EmployeeId,
        Title           = e.Title,
        Currency        = e.Currency,
        TravelRequestId = e.TravelRequestId,
        TotalAmount     = e.TotalAmount,
        TotalGst        = e.TotalGst,
        Status          = e.Status,
        SubmittedAt     = e.SubmittedAt,
        Notes           = e.Notes,
        CreatedAt       = e.CreatedAt,
        UpdatedAt       = e.UpdatedAt,
        Items           = e.Items.Select(i => new ExpenseItemDto
        {
            Id = i.Id, Category = i.Category, Description = i.Description,
            Amount = i.Amount, GstAmount = i.GstAmount, Currency = i.Currency,
            ExpenseDate = i.ExpenseDate, ReceiptPath = i.ReceiptPath, CreatedAt = i.CreatedAt
        }).ToList(),
        Attachments = e.Attachments.Select(a => new ExpenseAttachmentDto
        {
            Id = a.Id, FileName = a.FileName, FilePath = a.FilePath,
            FileSizeBytes = a.FileSizeBytes, CreatedAt = a.CreatedAt
        }).ToList(),
        Approvals = e.Approvals.Select(a => new ExpenseApprovalDto
        {
            Id = a.Id, Step = a.Step, Status = a.Status,
            ApproverName = a.ApproverName, Comments = a.Comments, ActionAt = a.ActionAt
        }).ToList(),
        History = e.History.OrderBy(h => h.CreatedAt).Select(h => new ExpenseHistoryDto
        {
            Id = h.Id, Action = h.Action, PreviousStatus = h.PreviousStatus,
            NewStatus = h.NewStatus, PerformedByName = h.PerformedByName,
            Remarks = h.Remarks, CreatedAt = h.CreatedAt
        }).ToList()
    };
}
