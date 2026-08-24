using HRMS.Application.Common;
using HRMS.Application.DTOs.Recruitment;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services;

public class RecruitmentService : IRecruitmentService
{
    private readonly ApplicationDbContext      _db;
    private readonly ILogger<RecruitmentService> _logger;

    public RecruitmentService(ApplicationDbContext db, ILogger<RecruitmentService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Helper: tenant-aware Where predicate ──────────────────────────────
    // FIX HIGH-SA4: companyId is now int? throughout this service.
    // null  → superadmin caller; skip the CompanyId filter so they see all companies.
    // value → scoped caller;    apply CompanyId == value for tenant isolation.
    private static bool MatchesCompany(int entityCompanyId, int? companyId) =>
        !companyId.HasValue || entityCompanyId == companyId.Value;

    // ── Dashboard ──────────────────────────────────────────────────────────
    public async Task<object> GetRecruitmentDashboardAsync(int? companyId)
    {
        var openReqs      = await _db.JobRequisitions.CountAsync(r => (!companyId.HasValue || r.CompanyId == companyId.Value) && r.Status == "Open");
        var totalCands    = await _db.Candidates.CountAsync(c => !companyId.HasValue || c.CompanyId == companyId.Value);
        var interviewed   = await _db.Candidates.CountAsync(c => (!companyId.HasValue || c.CompanyId == companyId.Value) && c.Status == "Interviewed");
        var hired         = await _db.Candidates.CountAsync(c => (!companyId.HasValue || c.CompanyId == companyId.Value) && c.Status == "Hired");
        var pendingOffers = await _db.OfferLetters.CountAsync(o => (!companyId.HasValue || o.CompanyId == companyId.Value) && o.Status == "Pending Approval");

        var byStatus = await _db.Candidates
            .Where(c => !companyId.HasValue || c.CompanyId == companyId.Value)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var recentInterviews = await _db.Interviews
            .Where(i => (!companyId.HasValue || i.CompanyId == companyId.Value) && i.ScheduledAt >= DateTime.UtcNow.AddDays(-7))
            .OrderByDescending(i => i.ScheduledAt)
            .Take(5)
            .Select(i => new { i.Id, i.ScheduledAt, i.InterviewType, i.Status })
            .ToListAsync();

        return new
        {
            OpenRequisitions   = openReqs,
            TotalCandidates    = totalCands,
            Interviewed        = interviewed,
            Hired              = hired,
            PendingOffers      = pendingOffers,
            CandidatesByStatus = byStatus,
            RecentInterviews   = recentInterviews,
        };
    }

    // ── Job Requisitions ───────────────────────────────────────────────────
    public async Task<List<RequisitionListDto>> ListRequisitionsAsync(int? companyId, string? status = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var q = _db.JobRequisitions.Where(r => !companyId.HasValue || r.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);

        var reqs = await q.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var ids  = reqs.Select(r => r.Id).ToList();
        var counts = await _db.Candidates
            .Where(c => (!companyId.HasValue || c.CompanyId == companyId.Value) && c.JobRequisitionId.HasValue && ids.Contains(c.JobRequisitionId.Value))
            .GroupBy(c => c.JobRequisitionId!.Value)
            .Select(g => new { ReqId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReqId, x => x.Count, ct);

        return reqs.Select(r => new RequisitionListDto
        {
            Id              = r.Id,
            Title           = r.Title,
            DepartmentName  = r.DepartmentName,
            OpeningsCount   = r.OpeningsCount,
            JobType         = r.JobType,
            Status          = r.Status,
            Location        = r.Location,
            ClosingDate     = r.ClosingDate,
            TotalCandidates = counts.TryGetValue(r.Id, out var c) ? c : 0,
            CreatedAt       = r.CreatedAt,
        }).ToList();
    }

    public async Task<PagedResult<RequisitionListDto>> ListRequisitionsPagedAsync(
        int? companyId, string? status = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.JobRequisitions
            .Where(r => !companyId.HasValue || r.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status);

        var totalCount = await q.CountAsync(ct);
        var reqs = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = reqs.Select(r => r.Id).ToList();
        var counts = ids.Count == 0
            ? new Dictionary<int, int>()
            : await _db.Candidates
                .Where(c => (!companyId.HasValue || c.CompanyId == companyId.Value)
                    && c.JobRequisitionId.HasValue
                    && ids.Contains(c.JobRequisitionId.Value))
                .GroupBy(c => c.JobRequisitionId!.Value)
                .Select(g => new { ReqId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ReqId, x => x.Count, ct);

        var items = reqs.Select(r => new RequisitionListDto
        {
            Id = r.Id,
            Title = r.Title,
            DepartmentName = r.DepartmentName,
            OpeningsCount = r.OpeningsCount,
            JobType = r.JobType,
            Status = r.Status,
            Location = r.Location,
            ClosingDate = r.ClosingDate,
            TotalCandidates = counts.TryGetValue(r.Id, out var count) ? count : 0,
            CreatedAt = r.CreatedAt,
        }).ToList();

        return PagedResult<RequisitionListDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<RequisitionDetailDto?> GetRequisitionAsync(int id, int? companyId)
    {
        var r = await _db.JobRequisitions.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (r is null) return null;

        var candidates = await ListCandidatesAsync(companyId, id, pageSize: int.MaxValue);
        return new RequisitionDetailDto
        {
            Id              = r.Id, Title = r.Title, DepartmentName = r.DepartmentName,
            OpeningsCount   = r.OpeningsCount, JobType = r.JobType, Status = r.Status,
            Location        = r.Location, ClosingDate = r.ClosingDate,
            TotalCandidates = candidates.TotalCount, CreatedAt = r.CreatedAt,
            Description     = r.Description, ExperienceRequired = r.ExperienceRequired,
            SkillsRequired  = r.SkillsRequired, MinSalary = r.MinSalary, MaxSalary = r.MaxSalary,
            CreatedByUserId = r.CreatedByUserId, Candidates = candidates.Items,
        };
    }

    public async Task<RequisitionListDto> CreateRequisitionAsync(CreateRequisitionDto dto, int? companyId, int userId)
    {
        var r = new JobRequisition
        {
            CompanyId          = companyId ?? 0, Title = dto.Title, DepartmentName = dto.DepartmentName,
            Description        = dto.Description, OpeningsCount = dto.OpeningsCount,
            ExperienceRequired = dto.ExperienceRequired, SkillsRequired = dto.SkillsRequired,
            JobType            = dto.JobType, MinSalary = dto.MinSalary, MaxSalary = dto.MaxSalary,
            Location           = dto.Location, ClosingDate = dto.ClosingDate, CreatedByUserId = userId,
        };
        _db.JobRequisitions.Add(r);
        await _db.SaveChangesAsync();
        return (await ListRequisitionsAsync(companyId)).First(x => x.Id == r.Id);
    }

    public async Task<RequisitionListDto> UpdateRequisitionAsync(int id, UpdateRequisitionDto dto, int? companyId)
    {
        var r = await _db.JobRequisitions.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value))
            ?? throw new KeyNotFoundException("Requisition not found.");
        r.Title = dto.Title; r.DepartmentName = dto.DepartmentName; r.Description = dto.Description;
        r.OpeningsCount = dto.OpeningsCount; r.ExperienceRequired = dto.ExperienceRequired;
        r.SkillsRequired = dto.SkillsRequired; r.JobType = dto.JobType;
        r.MinSalary = dto.MinSalary; r.MaxSalary = dto.MaxSalary;
        r.Location = dto.Location; r.ClosingDate = dto.ClosingDate; r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListRequisitionsAsync(companyId)).First(x => x.Id == r.Id);
    }

    public async Task<bool> UpdateRequisitionStatusAsync(int id, string status, int? companyId)
    {
        var r = await _db.JobRequisitions.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (r is null) return false;
        r.Status = status; r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteRequisitionAsync(int id, int? companyId)
    {
        var r = await _db.JobRequisitions.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (r is null) return false;
        _db.JobRequisitions.Remove(r); await _db.SaveChangesAsync(); return true;
    }

    // ── Candidates ─────────────────────────────────────────────────────────
    public async Task<PagedResult<CandidateListDto>> ListCandidatesAsync(int? companyId, int? requisitionId = null, string? status = null, int page = 1, int pageSize = 25, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default)
    {
        var q = _db.Candidates.AsNoTracking().Where(c => !companyId.HasValue || c.CompanyId == companyId.Value);
        if (requisitionId.HasValue) q = q.Where(c => c.JobRequisitionId == requisitionId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);

        _logger.LogInformation(
            "ListCandidatesAsync requested: sortBy={SortBy} sortDirection={SortDirection} page={Page} pageSize={PageSize}",
            sortBy, sortDirection, page, pageSize);

        bool desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var effectiveSortBy = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;
        q = effectiveSortBy switch
        {
            "candidatename" => desc ? q.OrderByDescending(c => c.FirstName)       : q.OrderBy(c => c.FirstName),
            "applieddate"   => desc ? q.OrderByDescending(c => c.CreatedAt)       : q.OrderBy(c => c.CreatedAt),
            "experience"    => desc ? q.OrderByDescending(c => c.TotalExperience) : q.OrderBy(c => c.TotalExperience),
            "status"        => desc ? q.OrderByDescending(c => c.Status)          : q.OrderBy(c => c.Status),
            _               => q.OrderByDescending(c => c.CreatedAt)
        };

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;
        var totalCount = await q.CountAsync(ct);
        int skip = pageSize == int.MaxValue ? 0 : (page - 1) * pageSize;
        int take = pageSize == int.MaxValue ? totalCount : pageSize;
        var list = await q.Skip(skip).Take(take).ToListAsync(ct);
        var reqIds = list.Where(c => c.JobRequisitionId.HasValue).Select(c => c.JobRequisitionId!.Value).Distinct().ToList();
        var reqTitles = await _db.JobRequisitions
            .Where(r => reqIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Title, ct);

        var items = list.Select(c => new CandidateListDto
        {
            Id = c.Id, FirstName = c.FirstName, LastName = c.LastName, Email = c.Email,
            Phone = c.Phone, CurrentDesignation = c.CurrentDesignation, TotalExperience = c.TotalExperience,
            Status = c.Status, SourceChannel = c.SourceChannel, JobRequisitionId = c.JobRequisitionId,
            JobTitle = c.JobRequisitionId.HasValue && reqTitles.TryGetValue(c.JobRequisitionId.Value, out var t) ? t : null,
            HasResume = !string.IsNullOrEmpty(c.ResumeFilePath), CreatedAt = c.CreatedAt,
        }).ToList();
        return PagedResult<CandidateListDto>.Create(items, totalCount, page, pageSize,
            sortBy: string.IsNullOrEmpty(effectiveSortBy) ? null : effectiveSortBy,
            sortDirection: desc ? "desc" : "asc");
    }

    public async Task<CandidateDetailDto?> GetCandidateAsync(int id, int? companyId)
    {
        var c = await _db.Candidates.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (c is null) return null;
        var interviews = await ListInterviewsAsync(companyId, id);
        // FIX HIGH-OOM1: GetCandidateAsync no longer calls the paginated ListOffersAsync.
        // Offers are fetched directly, scoped to this candidate, so loading all offers is not needed.
        var offers = await GetOffersForCandidateAsync(companyId, c.Id);
        return new CandidateDetailDto
        {
            Id = c.Id, FirstName = c.FirstName, LastName = c.LastName, Email = c.Email,
            Phone = c.Phone, CurrentDesignation = c.CurrentDesignation, TotalExperience = c.TotalExperience,
            Status = c.Status, SourceChannel = c.SourceChannel, JobRequisitionId = c.JobRequisitionId,
            HasResume = !string.IsNullOrEmpty(c.ResumeFilePath), ResumeFilePath = c.ResumeFilePath,
            Address = c.Address, CurrentCompany = c.CurrentCompany, Skills = c.Skills,
            QualificationSummary = c.QualificationSummary, Notes = c.Notes, CreatedAt = c.CreatedAt,
            Interviews = interviews, Offers = offers,
        };
    }

    public async Task<CandidateListDto> CreateCandidateAsync(CreateCandidateDto dto, string? resumeFilePath, int? companyId)
    {
        var c = new Candidate
        {
            CompanyId = companyId ?? 0, JobRequisitionId = dto.JobRequisitionId,
            FirstName = dto.FirstName, LastName = dto.LastName, Email = dto.Email,
            Phone = dto.Phone, Address = dto.Address, CurrentDesignation = dto.CurrentDesignation,
            CurrentCompany = dto.CurrentCompany, TotalExperience = dto.TotalExperience,
            Skills = dto.Skills, QualificationSummary = dto.QualificationSummary,
            ResumeFilePath = resumeFilePath, SourceChannel = dto.SourceChannel, Notes = dto.Notes,
        };
        _db.Candidates.Add(c); await _db.SaveChangesAsync();
        return (await ListCandidatesAsync(companyId, pageSize: int.MaxValue)).Items.First(x => x.Id == c.Id);
    }

    public async Task<CandidateListDto> UpdateCandidateAsync(int id, UpdateCandidateDto dto, string? resumeFilePath, int? companyId)
    {
        var c = await _db.Candidates.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value))
            ?? throw new KeyNotFoundException("Candidate not found.");
        c.JobRequisitionId = dto.JobRequisitionId; c.FirstName = dto.FirstName; c.LastName = dto.LastName;
        c.Email = dto.Email; c.Phone = dto.Phone; c.Address = dto.Address;
        c.CurrentDesignation = dto.CurrentDesignation; c.CurrentCompany = dto.CurrentCompany;
        c.TotalExperience = dto.TotalExperience; c.Skills = dto.Skills;
        c.QualificationSummary = dto.QualificationSummary; c.SourceChannel = dto.SourceChannel;
        c.Notes = dto.Notes; c.UpdatedAt = DateTime.UtcNow;
        if (resumeFilePath != null) c.ResumeFilePath = resumeFilePath;
        await _db.SaveChangesAsync();
        return (await ListCandidatesAsync(companyId, pageSize: int.MaxValue)).Items.First(x => x.Id == c.Id);
    }

    public async Task<bool> UpdateCandidateStatusAsync(int id, string status, string notes, int? companyId)
    {
        var c = await _db.Candidates.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (c is null) return false;
        c.Status = status;
        if (!string.IsNullOrWhiteSpace(notes)) c.Notes = string.IsNullOrWhiteSpace(c.Notes) ? notes : c.Notes + "\n" + notes;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteCandidateAsync(int id, int? companyId)
    {
        var c = await _db.Candidates.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (c is null) return false;
        _db.Candidates.Remove(c); await _db.SaveChangesAsync(); return true;
    }

    // ── Interviews ─────────────────────────────────────────────────────────
    public async Task<List<InterviewListDto>> ListInterviewsAsync(int? companyId, int? candidateId = null)
    {
        var q = _db.Interviews.Where(i => !companyId.HasValue || i.CompanyId == companyId.Value);
        if (candidateId.HasValue) q = q.Where(i => i.CandidateId == candidateId);
        var list = await q.OrderByDescending(i => i.ScheduledAt).ToListAsync();
        var cIds  = list.Select(i => i.CandidateId).Distinct().ToList();
        var cands = await _db.Candidates.Where(c => cIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());
        var reqIds  = list.Where(i => i.JobRequisitionId.HasValue).Select(i => i.JobRequisitionId!.Value).Distinct().ToList();
        var reqTitles = await _db.JobRequisitions.Where(r => reqIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Title);

        return list.Select(i => new InterviewListDto
        {
            Id = i.Id, CandidateId = i.CandidateId,
            CandidateName = cands.TryGetValue(i.CandidateId, out var cn) ? cn : "",
            JobRequisitionId = i.JobRequisitionId,
            JobTitle = i.JobRequisitionId.HasValue && reqTitles.TryGetValue(i.JobRequisitionId.Value, out var jt) ? jt : null,
            ScheduledAt = i.ScheduledAt, InterviewType = i.InterviewType, Venue = i.Venue,
            InterviewerNames = i.InterviewerNames, Status = i.Status, FeedbackScore = i.FeedbackScore,
            Recommendation = i.Recommendation, CreatedAt = i.CreatedAt,
        }).ToList();
    }

    public async Task<PagedResult<InterviewListDto>> ListInterviewsPagedAsync(
        int? companyId, int? candidateId = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Interviews
            .Where(i => !companyId.HasValue || i.CompanyId == companyId.Value);
        if (candidateId.HasValue)
            q = q.Where(i => i.CandidateId == candidateId.Value);

        var totalCount = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(i => i.ScheduledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var candidateIds = list.Select(i => i.CandidateId).Distinct().ToList();
        var candidates = candidateIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Candidates
                .Where(c => candidateIds.Contains(c.Id)
                    && (!companyId.HasValue || c.CompanyId == companyId.Value))
                .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim(), ct);

        var requisitionIds = list
            .Where(i => i.JobRequisitionId.HasValue)
            .Select(i => i.JobRequisitionId!.Value)
            .Distinct()
            .ToList();
        var requisitionTitles = requisitionIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.JobRequisitions
                .Where(r => requisitionIds.Contains(r.Id)
                    && (!companyId.HasValue || r.CompanyId == companyId.Value))
                .ToDictionaryAsync(r => r.Id, r => r.Title, ct);

        var items = list.Select(i => new InterviewListDto
        {
            Id = i.Id,
            CandidateId = i.CandidateId,
            CandidateName = candidates.TryGetValue(i.CandidateId, out var candidateName) ? candidateName : "",
            JobRequisitionId = i.JobRequisitionId,
            JobTitle = i.JobRequisitionId.HasValue
                && requisitionTitles.TryGetValue(i.JobRequisitionId.Value, out var title) ? title : null,
            ScheduledAt = i.ScheduledAt,
            InterviewType = i.InterviewType,
            Venue = i.Venue,
            InterviewerNames = i.InterviewerNames,
            Status = i.Status,
            FeedbackScore = i.FeedbackScore,
            Recommendation = i.Recommendation,
            CreatedAt = i.CreatedAt,
        }).ToList();

        return PagedResult<InterviewListDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<InterviewListDto> ScheduleInterviewAsync(ScheduleInterviewDto dto, int? companyId, int userId)
    {
        var i = new Interview
        {
            CompanyId = companyId ?? 0, CandidateId = dto.CandidateId, JobRequisitionId = dto.JobRequisitionId,
            ScheduledAt = dto.ScheduledAt, InterviewType = dto.InterviewType, Venue = dto.Venue,
            InterviewerNames = dto.InterviewerNames, CreatedByUserId = userId,
        };
        _db.Interviews.Add(i); await _db.SaveChangesAsync();
        return (await ListInterviewsAsync(companyId)).First(x => x.Id == i.Id);
    }

    public async Task<InterviewListDto> UpdateInterviewAsync(int id, UpdateInterviewDto dto, int? companyId)
    {
        var i = await _db.Interviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value))
            ?? throw new KeyNotFoundException("Interview not found.");
        i.ScheduledAt = dto.ScheduledAt; i.InterviewType = dto.InterviewType;
        i.Venue = dto.Venue; i.InterviewerNames = dto.InterviewerNames;
        i.Status = dto.Status; i.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await ListInterviewsAsync(companyId)).First(x => x.Id == i.Id);
    }

    public async Task<bool> SubmitInterviewFeedbackAsync(int id, SubmitFeedbackDto dto, int? companyId)
    {
        var i = await _db.Interviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (i is null) return false;
        i.FeedbackScore = dto.FeedbackScore; i.FeedbackNotes = dto.FeedbackNotes;
        i.Recommendation = dto.Recommendation; i.Status = dto.Status; i.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> DeleteInterviewAsync(int id, int? companyId)
    {
        var i = await _db.Interviews.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (i is null) return false;
        _db.Interviews.Remove(i); await _db.SaveChangesAsync(); return true;
    }

    // ── Offer Letters ──────────────────────────────────────────────────────

    // FIX HIGH-OOM1: ListOffersAsync now paginates results instead of loading all rows.
    // Callers that previously loaded all offers for a single candidate should use
    // GetOffersForCandidateAsync instead (private helper below).
    public async Task<PagedResult<OfferListDto>> ListOffersAsync(int? companyId, int? candidateId = null, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 25;

        var q = _db.OfferLetters.AsNoTracking().Where(o => !companyId.HasValue || o.CompanyId == companyId.Value);
        if (candidateId.HasValue) q = q.Where(o => o.CandidateId == candidateId);
        q = q.OrderByDescending(o => o.CreatedAt);

        var totalCount = await q.CountAsync();
        var list = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var items = await MapOfferListAsync(list);
        return PagedResult<OfferListDto>.Create(items, totalCount, page, pageSize);
    }

    // FIX HIGH-OOM1: GetOfferAsync queries by ID directly — no longer loads all offers.
    public async Task<OfferListDto?> GetOfferAsync(int id, int? companyId)
    {
        var o = await _db.OfferLetters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (o is null) return null;
        var mapped = await MapOfferListAsync(new List<OfferLetter> { o });
        return mapped.FirstOrDefault();
    }

    // FIX HIGH-OOM1: CreateOfferAsync fetches the new record by ID directly.
    public async Task<OfferListDto> CreateOfferAsync(CreateOfferDto dto, int? companyId, int userId)
    {
        var o = new OfferLetter
        {
            CompanyId = companyId ?? 0, CandidateId = dto.CandidateId, JobRequisitionId = dto.JobRequisitionId,
            OfferedDesignation = dto.OfferedDesignation, OfferedDepartment = dto.OfferedDepartment,
            OfferedSalary = dto.OfferedSalary, JoiningDate = dto.JoiningDate,
            ExpiryDate = dto.ExpiryDate, CreatedByUserId = userId,
        };
        _db.OfferLetters.Add(o); await _db.SaveChangesAsync();
        await UpdateCandidateStatusAsync(dto.CandidateId, "Offer Extended", "Offer letter created", companyId);
        var result = await GetOfferAsync(o.Id, companyId);
        return result!;
    }

    public async Task<bool> ApproveOfferAsync(int id, ApproveOfferDto dto, int? companyId, int userId)
    {
        var o = await _db.OfferLetters.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (o is null) return false;
        o.Status = "Approved"; o.ApprovedByUserId = userId; o.ApprovalNotes = dto.ApprovalNotes;
        o.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return true;
    }

    public async Task<bool> UpdateOfferStatusAsync(int id, string status, int? companyId)
    {
        var o = await _db.OfferLetters.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (o is null) return false;
        o.Status = status; o.UpdatedAt = DateTime.UtcNow;
        if (status == "Accepted") await UpdateCandidateStatusAsync(o.CandidateId, "Hired",     "Offer accepted", companyId);
        if (status == "Rejected") await UpdateCandidateStatusAsync(o.CandidateId, "Rejected",  "Offer rejected", companyId);
        await _db.SaveChangesAsync(); return true;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Maps a pre-fetched list of OfferLetter entities to DTOs, enriching with candidate and
    /// requisition names. Keeps join logic in one place so pagination and single-row fetches
    /// both use the same mapping without redundant queries.
    /// </summary>
    private async Task<List<OfferListDto>> MapOfferListAsync(List<OfferLetter> list)
    {
        var cIds      = list.Select(o => o.CandidateId).Distinct().ToList();
        var cands     = await _db.Candidates.Where(c => cIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());
        var reqIds    = list.Where(o => o.JobRequisitionId.HasValue).Select(o => o.JobRequisitionId!.Value).Distinct().ToList();
        var reqTitles = await _db.JobRequisitions.Where(r => reqIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Title);

        return list.Select(o => new OfferListDto
        {
            Id = o.Id, CandidateId = o.CandidateId,
            CandidateName = cands.TryGetValue(o.CandidateId, out var cn) ? cn : "",
            JobRequisitionId = o.JobRequisitionId,
            JobTitle = o.JobRequisitionId.HasValue && reqTitles.TryGetValue(o.JobRequisitionId.Value, out var jt) ? jt : null,
            OfferedDesignation = o.OfferedDesignation, OfferedDepartment = o.OfferedDepartment,
            OfferedSalary = o.OfferedSalary, JoiningDate = o.JoiningDate, ExpiryDate = o.ExpiryDate,
            Status = o.Status, CreatedAt = o.CreatedAt,
        }).ToList();
    }

    /// <summary>
    /// Returns all offers for a specific candidate without pagination.
    /// Used for CandidateDetailDto enrichment only — the result set is always
    /// bounded by candidateId so unbounded loading is not a risk here.
    /// </summary>
    private async Task<List<OfferListDto>> GetOffersForCandidateAsync(int? companyId, int candidateId)
    {
        var list = await _db.OfferLetters.AsNoTracking()
            .Where(o => o.CandidateId == candidateId && (!companyId.HasValue || o.CompanyId == companyId.Value))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return await MapOfferListAsync(list);
    }
}
