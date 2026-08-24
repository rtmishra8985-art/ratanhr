using HRMS.Application.Common;
using HRMS.Application.DTOs.Performance;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Performance;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class PerformanceService : IPerformanceService
{
    private readonly ApplicationDbContext     _db;
    private readonly ILogger<PerformanceService> _logger;

    public PerformanceService(ApplicationDbContext db, ILogger<PerformanceService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Dashboard ──────────────────────────────────────────────────────────
    // FIX HIGH-SA4: companyId is now int? — null means superadmin (all companies).
    public async Task<object> GetPerformanceDashboardAsync(int? companyId)
    {
        var activeCycles    = await _db.PerformanceCycles.CountAsync(c => (!companyId.HasValue || c.CompanyId == companyId.Value) && c.Status == "Active");
        var totalGoals      = await _db.EmployeeGoals.CountAsync(g => !companyId.HasValue || g.CompanyId == companyId.Value);
        var completedGoals  = await _db.EmployeeGoals.CountAsync(g => (!companyId.HasValue || g.CompanyId == companyId.Value) && g.Status == "Completed");
        var pendingReviews  = await _db.PerformanceReviews.CountAsync(r => (!companyId.HasValue || r.CompanyId == companyId.Value) && r.Status == "Pending");
        var submittedReviews = await _db.PerformanceReviews.CountAsync(r => (!companyId.HasValue || r.CompanyId == companyId.Value) && r.Status == "Submitted");
        var avgRating = await _db.PerformanceReviews
            .Where(r => (!companyId.HasValue || r.CompanyId == companyId.Value) && r.FinalRating.HasValue)
            .AverageAsync(r => (double?)r.FinalRating) ?? 0;

        var goalsByStatus = await _db.EmployeeGoals
            .Where(g => !companyId.HasValue || g.CompanyId == companyId.Value)
            .GroupBy(g => g.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var ratingDist = await _db.PerformanceReviews
            .Where(r => (!companyId.HasValue || r.CompanyId == companyId.Value) && r.FinalRating.HasValue)
            .GroupBy(r => (int)Math.Floor((double)r.FinalRating!.Value))
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync();

        return new
        {
            ActiveCycles       = activeCycles,
            TotalGoals         = totalGoals,
            CompletedGoals     = completedGoals,
            PendingReviews     = pendingReviews,
            SubmittedReviews   = submittedReviews,
            AverageRating      = Math.Round(avgRating, 2),
            GoalsByStatus      = goalsByStatus,
            RatingDistribution = ratingDist,
        };
    }

    // ── Performance Cycles ─────────────────────────────────────────────────
    // FIX HIGH-OOM2: ListCyclesAsync now returns PagedResult<CycleListDto> to prevent
    // full-table loads that could OOM the server for companies with many cycles.
    // FIX HIGH-SA4: companyId is now int?
    public async Task<PagedResult<CycleListDto>> ListCyclesAsync(int? companyId, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;

        var q = _db.PerformanceCycles.AsNoTracking()
            .Where(c => !companyId.HasValue || c.CompanyId == companyId.Value)
            .OrderByDescending(c => c.StartDate);

        var totalCount = await q.CountAsync();
        var cycles = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = cycles.Select(c => c.Id).ToList();

        var reviewCounts = await _db.PerformanceReviews
            .Where(r => (!companyId.HasValue || r.CompanyId == companyId.Value) && r.PerformanceCycleId.HasValue && ids.Contains(r.PerformanceCycleId.Value))
            .GroupBy(r => r.PerformanceCycleId!.Value)
            .Select(g => new { CycleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CycleId, x => x.Count);
        var goalCounts = await _db.EmployeeGoals
            .Where(g => (!companyId.HasValue || g.CompanyId == companyId.Value) && g.PerformanceCycleId.HasValue && ids.Contains(g.PerformanceCycleId.Value))
            .GroupBy(g => g.PerformanceCycleId!.Value)
            .Select(g => new { CycleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CycleId, x => x.Count);

        var items = cycles.Select(c => new CycleListDto
        {
            Id = c.Id, Name = c.Name, StartDate = c.StartDate, EndDate = c.EndDate,
            Status = c.Status, ReviewType = c.ReviewType, CreatedAt = c.CreatedAt,
            TotalReviews = reviewCounts.TryGetValue(c.Id, out var rc) ? rc : 0,
            TotalGoals   = goalCounts.TryGetValue(c.Id, out var gc) ? gc : 0,
        }).ToList();

        return PagedResult<CycleListDto>.Create(items, totalCount, page, pageSize);
    }

    // FIX HIGH-OOM2 / SA4: CreateCycleAsync now fetches the new cycle by ID directly
    // instead of calling ListCyclesAsync (which is now paginated).
    public async Task<CycleListDto> CreateCycleAsync(CreateCycleDto dto, int? companyId, int userId)
    {
        var c = new PerformanceCycle
        {
            CompanyId = companyId ?? 0, Name = dto.Name, StartDate = dto.StartDate,
            EndDate = dto.EndDate, ReviewType = dto.ReviewType, CreatedByUserId = userId,
        };
        _db.PerformanceCycles.Add(c); await _db.SaveChangesAsync();
        return await GetCycleByIdAsync(c.Id, companyId)
            ?? throw new InvalidOperationException("Cycle not found after creation.");
    }

    // FIX HIGH-OOM2 / SA4: UpdateCycleAsync fetches by ID directly.
    public async Task<CycleListDto> UpdateCycleAsync(int id, UpdateCycleDto dto, int? companyId)
    {
        var c = await _db.PerformanceCycles.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value))
            ?? throw new KeyNotFoundException("Cycle not found.");
        c.Name = dto.Name; c.StartDate = dto.StartDate; c.EndDate = dto.EndDate;
        c.ReviewType = dto.ReviewType; c.Status = dto.Status; c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetCycleByIdAsync(c.Id, companyId)
            ?? throw new InvalidOperationException("Cycle not found after update.");
    }

    public async Task<bool> DeleteCycleAsync(int id, int? companyId)
    {
        var c = await _db.PerformanceCycles.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (c is null) return false;
        _db.PerformanceCycles.Remove(c); await _db.SaveChangesAsync(); return true;
    }

    // ── Employee Goals ─────────────────────────────────────────────────────
    public async Task<PagedGoalsDto> ListGoalsAsync(
        int? companyId, string? employeeId = null, int? cycleId = null,
        int page = 1, int pageSize = 20, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default)
    {
        var q = _db.EmployeeGoals.AsNoTracking().Where(g => !companyId.HasValue || g.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(g => g.EmployeeId == employeeId);
        if (cycleId.HasValue) q = q.Where(g => g.PerformanceCycleId == cycleId);

        _logger.LogInformation(
            "ListGoalsAsync requested: sortBy={SortBy} sortDirection={SortDirection} page={Page} pageSize={PageSize}",
            sortBy, sortDirection, page, pageSize);

        bool desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var effectiveSortBy = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;
        q = effectiveSortBy switch
        {
            "goaltitle"            => desc ? q.OrderByDescending(g => g.Title)        : q.OrderBy(g => g.Title),
            "employeename"         => desc
                ? q.OrderByDescending(g => _db.Employees.Where(e => e.EmployeeCode == g.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? "")
                : q.OrderBy(g => _db.Employees.Where(e => e.EmployeeCode == g.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? ""),
            "weightage"            => desc ? q.OrderByDescending(g => g.Weight)       : q.OrderBy(g => g.Weight),
            "targetdate"           => desc ? q.OrderByDescending(g => g.DueDate)      : q.OrderBy(g => g.DueDate),
            "completionpercentage" => desc ? q.OrderByDescending(g => g.AchievedValue): q.OrderBy(g => g.AchievedValue),
            "createddate"          => desc ? q.OrderByDescending(g => g.CreatedAt)    : q.OrderBy(g => g.CreatedAt),
            "status"               => desc ? q.OrderByDescending(g => g.Status)       : q.OrderBy(g => g.Status),
            _                      => q.OrderByDescending(g => g.CreatedAt)
        };

        var total = await q.CountAsync(ct);
        var list  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var cycleIds   = list.Where(g => g.PerformanceCycleId.HasValue).Select(g => g.PerformanceCycleId!.Value).Distinct().ToList();
        var cycleNames = await _db.PerformanceCycles
            .Where(c => cycleIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var items = list.Select(g => new GoalListDto
        {
            Id = g.Id, EmployeeId = g.EmployeeId, Title = g.Title, GoalType = g.GoalType,
            Category = g.Category, TargetValue = g.TargetValue, AchievedValue = g.AchievedValue,
            Unit = g.Unit, DueDate = g.DueDate, Status = g.Status, Weight = g.Weight,
            CycleName = g.PerformanceCycleId.HasValue && cycleNames.TryGetValue(g.PerformanceCycleId.Value, out var cn) ? cn : null,
            CreatedAt = g.CreatedAt,
        }).ToList();

        return new PagedGoalsDto
        {
            Items         = items,
            TotalCount    = total,
            Page          = page,
            PageSize      = pageSize,
            SortBy        = string.IsNullOrEmpty(effectiveSortBy) ? null : effectiveSortBy,
            SortDirection = desc ? "desc" : "asc",
        };
    }

    public async Task<GoalListDto> CreateGoalAsync(CreateGoalDto dto, int? companyId, int userId)
    {
        var g = new EmployeeGoal
        {
            CompanyId = companyId ?? 0, EmployeeId = dto.EmployeeId, PerformanceCycleId = dto.PerformanceCycleId,
            Title = dto.Title, Description = dto.Description, GoalType = dto.GoalType,
            Category = dto.Category, TargetValue = dto.TargetValue, Unit = dto.Unit,
            DueDate = dto.DueDate, Weight = dto.Weight, CreatedByUserId = userId,
        };
        _db.EmployeeGoals.Add(g); await _db.SaveChangesAsync();
        return (await ListGoalsAsync(companyId, g.EmployeeId)).Items.First(x => x.Id == g.Id);
    }

    public async Task<GoalListDto> UpdateGoalAsync(int id, UpdateGoalDto dto, int? companyId)
    {
        var g = await _db.EmployeeGoals.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value))
            ?? throw new KeyNotFoundException("Goal not found.");
        g.Title = dto.Title; g.Description = dto.Description; g.GoalType = dto.GoalType;
        g.Category = dto.Category; g.TargetValue = dto.TargetValue; g.Unit = dto.Unit;
        g.DueDate = dto.DueDate; g.Weight = dto.Weight; g.Status = dto.Status; g.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListGoalsAsync(companyId, g.EmployeeId)).Items.First(x => x.Id == g.Id);
    }

    public async Task<bool> UpdateGoalProgressAsync(int id, decimal achievedValue, int? companyId, string? callerEmployeeId = null)
    {
        var g = await _db.EmployeeGoals.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (g is null) return false;
        if (!string.IsNullOrEmpty(callerEmployeeId) && g.EmployeeId != callerEmployeeId)
            return false;
        g.AchievedValue = achievedValue; g.UpdatedAt = DateTime.UtcNow;
        if (achievedValue >= g.TargetValue) g.Status = "Completed";
        else if (achievedValue > 0) g.Status = "In Progress";
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteGoalAsync(int id, int? companyId)
    {
        var g = await _db.EmployeeGoals.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (g is null) return false;
        _db.EmployeeGoals.Remove(g); await _db.SaveChangesAsync(); return true;
    }

    // ── Performance Reviews ────────────────────────────────────────────────
    // FIX [MEMORY]: Previously loaded ALL reviews into memory then paginated in-controller.
    // Now applies Skip/Take at the DB layer — only the requested page is transferred.
    public async Task<PagedResult<ReviewListDto>> ListReviewsAsync(
        int? companyId, string? employeeId = null, int? cycleId = null,
        int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;

        var q = _db.PerformanceReviews.Where(r => !companyId.HasValue || r.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(r => r.EmployeeId == employeeId);
        if (cycleId.HasValue) q = q.Where(r => r.PerformanceCycleId == cycleId);

        var totalCount = await q.CountAsync();
        var list = await q.OrderByDescending(r => r.CreatedAt)
                          .Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToListAsync();

        var cycleIds   = list.Where(r => r.PerformanceCycleId.HasValue).Select(r => r.PerformanceCycleId!.Value).Distinct().ToList();
        var cycleNames = cycleIds.Any()
            ? await _db.PerformanceCycles.Where(c => cycleIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name)
            : new Dictionary<int, string>();

        var items = list.Select(r => new ReviewListDto
        {
            Id = r.Id, EmployeeId = r.EmployeeId, ReviewType = r.ReviewType, Status = r.Status,
            SelfRating = r.SelfRating, ManagerRating = r.ManagerRating, FinalRating = r.FinalRating,
            SubmittedAt = r.SubmittedAt, CreatedAt = r.CreatedAt,
            CycleName = r.PerformanceCycleId.HasValue && cycleNames.TryGetValue(r.PerformanceCycleId.Value, out var cn) ? cn : null,
        }).ToList();

        return PagedResult<ReviewListDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<ReviewDetailDto?> GetReviewAsync(int id, int? companyId)
    {
        var r = await _db.PerformanceReviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (r is null) return null;
        string? cycleName = null;
        if (r.PerformanceCycleId.HasValue)
            cycleName = (await _db.PerformanceCycles.FirstOrDefaultAsync(x => x.Id == r.PerformanceCycleId.Value))?.Name;
        return new ReviewDetailDto
        {
            Id = r.Id, EmployeeId = r.EmployeeId, ReviewType = r.ReviewType, Status = r.Status,
            SelfRating = r.SelfRating, ManagerRating = r.ManagerRating, FinalRating = r.FinalRating,
            SubmittedAt = r.SubmittedAt, CreatedAt = r.CreatedAt, CycleName = cycleName,
            SelfComments = r.SelfComments, ManagerComments = r.ManagerComments,
            HrComments = r.HrComments, OverallComments = r.OverallComments,
            AcknowledgedAt = r.AcknowledgedAt,
        };
    }

    public async Task<ReviewListDto> CreateReviewAsync(CreateReviewDto dto, int? companyId)
    {
        var r = new PerformanceReview
        {
            CompanyId = companyId ?? 0, EmployeeId = dto.EmployeeId, ReviewerId = dto.ReviewerId,
            PerformanceCycleId = dto.PerformanceCycleId, ReviewType = dto.ReviewType,
        };
        _db.PerformanceReviews.Add(r); await _db.SaveChangesAsync();
        var paged = await ListReviewsAsync(companyId, page: 1, pageSize: 200);
        var created = paged.Items.FirstOrDefault(x => x.Id == r.Id);
        if (created is null)
            throw new InvalidOperationException("The performance review was created but could not be reloaded.");
        return created;
    }

    public async Task<bool> SubmitSelfReviewAsync(int id, SubmitSelfReviewDto dto, int? companyId, string employeeId)
    {
        var r = await _db.PerformanceReviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value) && x.EmployeeId == employeeId);
        if (r is null) return false;
        r.SelfRating = dto.SelfRating; r.SelfComments = dto.SelfComments; r.OverallComments = dto.OverallComments;
        r.Status = "In Progress"; r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> SubmitManagerReviewAsync(int id, SubmitManagerReviewDto dto, int? companyId)
    {
        var r = await _db.PerformanceReviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (r is null) return false;
        r.ManagerRating = dto.ManagerRating; r.ManagerComments = dto.ManagerComments;
        r.Status = "Submitted"; r.SubmittedAt = DateTime.UtcNow; r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> FinalizeReviewAsync(int id, FinalizeReviewDto dto, int? companyId)
    {
        var r = await _db.PerformanceReviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (r is null) return false;
        r.FinalRating = dto.FinalRating; r.HrComments = dto.HrComments;
        r.Status = "Acknowledged"; r.AcknowledgedAt = DateTime.UtcNow; r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    // ── Continuous Feedback ────────────────────────────────────────────────
    // FIX HIGH-OOM3: ListFeedbackAsync now returns PagedResult<FeedbackListDto> to prevent
    // full-table loads that could OOM the server.
    // FIX HIGH-SA4: companyId is now int?
    public async Task<PagedResult<FeedbackListDto>> ListFeedbackAsync(int? companyId, string? toEmployeeId = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;

        var q = _db.ContinuousFeedbacks.AsNoTracking()
            .Where(f => !companyId.HasValue || f.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(toEmployeeId)) q = q.Where(f => f.ToEmployeeId == toEmployeeId);
        q = q.OrderByDescending(f => f.CreatedAt);

        var totalCount = await q.CountAsync();
        var list = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var items = list.Select(f => new FeedbackListDto
        {
            Id = f.Id, FromEmployeeId = f.IsAnonymous ? "Anonymous" : f.FromEmployeeId,
            ToEmployeeId = f.ToEmployeeId, FeedbackText = f.FeedbackText,
            FeedbackType = f.FeedbackType, IsAnonymous = f.IsAnonymous, CreatedAt = f.CreatedAt,
        }).ToList();

        return PagedResult<FeedbackListDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<FeedbackListDto> SubmitFeedbackAsync(CreateFeedbackDto dto, int? companyId, string fromEmployeeId)
    {
        var f = new ContinuousFeedback
        {
            CompanyId = companyId ?? 0, FromEmployeeId = fromEmployeeId, ToEmployeeId = dto.ToEmployeeId,
            FeedbackText = dto.FeedbackText, FeedbackType = dto.FeedbackType, IsAnonymous = dto.IsAnonymous,
        };
        _db.ContinuousFeedbacks.Add(f); await _db.SaveChangesAsync();
        return new FeedbackListDto
        {
            Id = f.Id, FromEmployeeId = f.IsAnonymous ? "Anonymous" : f.FromEmployeeId,
            ToEmployeeId = f.ToEmployeeId, FeedbackText = f.FeedbackText,
            FeedbackType = f.FeedbackType, IsAnonymous = f.IsAnonymous, CreatedAt = f.CreatedAt,
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a single CycleListDto by primary key without loading a full page.
    /// Used by CreateCycleAsync and UpdateCycleAsync after saving to return the saved record.
    /// </summary>
    private async Task<CycleListDto?> GetCycleByIdAsync(int id, int? companyId)
    {
        var c = await _db.PerformanceCycles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (c is null) return null;

        var reviewCount = await _db.PerformanceReviews
            .CountAsync(r => r.PerformanceCycleId == id && (!companyId.HasValue || r.CompanyId == companyId.Value));
        var goalCount = await _db.EmployeeGoals
            .CountAsync(g => g.PerformanceCycleId == id && (!companyId.HasValue || g.CompanyId == companyId.Value));

        return new CycleListDto
        {
            Id = c.Id, Name = c.Name, StartDate = c.StartDate, EndDate = c.EndDate,
            Status = c.Status, ReviewType = c.ReviewType, CreatedAt = c.CreatedAt,
            TotalReviews = reviewCount,
            TotalGoals   = goalCount,
        };
    }
}
