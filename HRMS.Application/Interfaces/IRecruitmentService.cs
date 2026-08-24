using HRMS.Application.Common;
using HRMS.Application.DTOs.Recruitment;

namespace HRMS.Application.Interfaces;

public interface IRecruitmentService
{
    // Dashboard
    // FIX HIGH-SA4: companyId is now int? — null means superadmin (all companies).
    Task<object> GetRecruitmentDashboardAsync(int? companyId);

    // Job Requisitions
    Task<List<RequisitionListDto>> ListRequisitionsAsync(int? companyId, string? status = null, CancellationToken ct = default);
    Task<PagedResult<RequisitionListDto>> ListRequisitionsPagedAsync(
        int? companyId, string? status = null, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<RequisitionDetailDto?> GetRequisitionAsync(int id, int? companyId);
    Task<RequisitionListDto> CreateRequisitionAsync(CreateRequisitionDto dto, int? companyId, int userId);
    Task<RequisitionListDto> UpdateRequisitionAsync(int id, UpdateRequisitionDto dto, int? companyId);
    Task<bool> UpdateRequisitionStatusAsync(int id, string status, int? companyId);
    Task<bool> DeleteRequisitionAsync(int id, int? companyId);

    // Candidates
    // FIX HIGH-SA4: companyId is now int?
    Task<PagedResult<CandidateListDto>> ListCandidatesAsync(int? companyId, int? requisitionId = null, string? status = null, int page = 1, int pageSize = 25, string? sortBy = null, string? sortDirection = "desc", CancellationToken ct = default);
    Task<CandidateDetailDto?> GetCandidateAsync(int id, int? companyId);
    Task<CandidateListDto> CreateCandidateAsync(CreateCandidateDto dto, string? resumeFilePath, int? companyId);
    Task<CandidateListDto> UpdateCandidateAsync(int id, UpdateCandidateDto dto, string? resumeFilePath, int? companyId);
    Task<bool> UpdateCandidateStatusAsync(int id, string status, string notes, int? companyId);
    Task<bool> DeleteCandidateAsync(int id, int? companyId);

    // Interviews
    Task<List<InterviewListDto>> ListInterviewsAsync(int? companyId, int? candidateId = null);
    Task<PagedResult<InterviewListDto>> ListInterviewsPagedAsync(
        int? companyId, int? candidateId = null, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<InterviewListDto> ScheduleInterviewAsync(ScheduleInterviewDto dto, int? companyId, int userId);
    Task<InterviewListDto> UpdateInterviewAsync(int id, UpdateInterviewDto dto, int? companyId);
    Task<bool> SubmitInterviewFeedbackAsync(int id, SubmitFeedbackDto dto, int? companyId);
    Task<bool> DeleteInterviewAsync(int id, int? companyId);

    // Offer Letters
    // FIX HIGH-OOM1: ListOffersAsync now returns PagedResult to prevent unbounded row load.
    Task<PagedResult<OfferListDto>> ListOffersAsync(int? companyId, int? candidateId = null, int page = 1, int pageSize = 25);
    Task<OfferListDto?> GetOfferAsync(int id, int? companyId);
    Task<OfferListDto> CreateOfferAsync(CreateOfferDto dto, int? companyId, int userId);
    Task<bool> ApproveOfferAsync(int id, ApproveOfferDto dto, int? companyId, int userId);
    Task<bool> UpdateOfferStatusAsync(int id, string status, int? companyId);
}
