using HRMS.Application.Common;
using HRMS.Application.DTOs.Performance;

namespace HRMS.Application.Interfaces;

public interface IPerformanceService
{
    // Dashboard
    // FIX HIGH-SA4: companyId is now int? — null means superadmin (all companies).
    Task<object> GetPerformanceDashboardAsync(int? companyId);

    // Performance Cycles
    // FIX HIGH-OOM2: ListCyclesAsync now returns PagedResult to prevent unbounded row load.
    // FIX HIGH-SA4: companyId is now int?
    Task<PagedResult<CycleListDto>> ListCyclesAsync(int? companyId, int page = 1, int pageSize = 25);
    Task<CycleListDto> CreateCycleAsync(CreateCycleDto dto, int? companyId, int userId);
    Task<CycleListDto> UpdateCycleAsync(int id, UpdateCycleDto dto, int? companyId);
    Task<bool> DeleteCycleAsync(int id, int? companyId);

    // Employee Goals
    Task<PagedGoalsDto> ListGoalsAsync(int? companyId, string? employeeId = null, int? cycleId = null, int page = 1, int pageSize = 20, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default);
    Task<GoalListDto> CreateGoalAsync(CreateGoalDto dto, int? companyId, int userId);
    Task<GoalListDto> UpdateGoalAsync(int id, UpdateGoalDto dto, int? companyId);
    Task<bool> UpdateGoalProgressAsync(int id, decimal achievedValue, int? companyId, string? callerEmployeeId = null);
    Task<bool> DeleteGoalAsync(int id, int? companyId);

    // Performance Reviews
    /// <summary>
    /// Returns a DB-paginated page of reviews. Skip/Take applied at the database layer —
    /// never loads the full result set into memory regardless of tenant size.
    /// </summary>
    Task<PagedResult<ReviewListDto>> ListReviewsAsync(
        int? companyId, string? employeeId = null, int? cycleId = null,
        int page = 1, int pageSize = 25);
    Task<ReviewDetailDto?> GetReviewAsync(int id, int? companyId);
    Task<ReviewListDto> CreateReviewAsync(CreateReviewDto dto, int? companyId);
    Task<bool> SubmitSelfReviewAsync(int id, SubmitSelfReviewDto dto, int? companyId, string employeeId);
    Task<bool> SubmitManagerReviewAsync(int id, SubmitManagerReviewDto dto, int? companyId);
    Task<bool> FinalizeReviewAsync(int id, FinalizeReviewDto dto, int? companyId);

    // Continuous Feedback
    // FIX HIGH-OOM3: ListFeedbackAsync now returns PagedResult to prevent unbounded row load.
    Task<PagedResult<FeedbackListDto>> ListFeedbackAsync(int? companyId, string? toEmployeeId = null, int page = 1, int pageSize = 25);
    Task<FeedbackListDto> SubmitFeedbackAsync(CreateFeedbackDto dto, int? companyId, string fromEmployeeId);
}
