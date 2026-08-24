using HRMS.Application.DTOs.Sales;
using HRMS.Application.Common;

namespace HRMS.Application.Interfaces;

public interface ISalesService
{
    // ── Dashboard ─────────────────────────────────────────────────────────
    /// <summary>null = SuperAdmin cross-company view</summary>
    Task<object> GetSalesDashboardAsync(int? companyId);

    // ── Leads ─────────────────────────────────────────────────────────────
    Task<(List<LeadListDto> Items, int Total)> ListLeadsAsync(
        int? companyId, int page, int pageSize, string? status = null, string? search = null);
    Task<LeadDetailDto?> GetLeadAsync(int id, int? companyId);
    Task<LeadListDto> CreateLeadAsync(CreateLeadDto dto, int companyId, int userId);
    Task<LeadListDto> UpdateLeadAsync(int id, UpdateLeadDto dto, int companyId);
    Task<bool> UpdateLeadStatusAsync(int id, string status, int companyId);
    Task<bool> DeleteLeadAsync(int id, int companyId);

    // ── Lead Assignment ───────────────────────────────────────────────────
    Task<LeadListDto> AssignLeadAsync(int leadId, AssignLeadDto dto, int companyId, int assignedByUserId);
    Task<LeadListDto> ReassignLeadAsync(int leadId, ReassignLeadDto dto, int companyId, int assignedByUserId);
    Task<int> BulkAssignLeadsAsync(BulkAssignLeadsDto dto, int companyId, int assignedByUserId);
    Task<List<LeadAssignmentHistoryDto>> GetLeadAssignmentHistoryAsync(int leadId, int? companyId);
    Task<(List<LeadListDto> Items, int Total)> GetMyAssignedLeadsAsync(string employeeId, int? companyId, int page, int pageSize);
    Task<(List<LeadListDto> Items, int Total)> GetUnassignedLeadsAsync(int? companyId, int page, int pageSize);
    Task<(List<LeadListDto> Items, int Total)> GetTeamLeadsAsync(string managerId, int? companyId, int page, int pageSize);

    // ── Customers ─────────────────────────────────────────────────────────
    Task<(List<CustomerListDto> Items, int Total)> ListCustomersAsync(
        int? companyId, int page, int pageSize, string? search = null);
    Task<CustomerDetailDto?> GetCustomerAsync(int id, int? companyId);
    Task<CustomerListDto> CreateCustomerAsync(CreateCustomerDto dto, int companyId, int userId);
    /// <summary>Convert an existing lead into a customer record.</summary>
    Task<CustomerListDto> ConvertLeadToCustomerAsync(int leadId, CreateCustomerDto dto, int companyId, int userId);
    Task<CustomerListDto> UpdateCustomerAsync(int id, UpdateCustomerDto dto, int companyId);
    Task<bool> DeleteCustomerAsync(int id, int companyId);

    // ── Follow-Ups ────────────────────────────────────────────────────────
    Task<List<FollowUpListDto>> ListFollowUpsAsync(
        int? companyId, int? leadId = null, string? status = null);
    Task<PagedResult<FollowUpListDto>> ListFollowUpsPagedAsync(
        int? companyId, int? leadId = null, string? status = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc");
    Task<FollowUpListDto> CreateFollowUpAsync(CreateFollowUpDto dto, int companyId, int userId);
    Task<FollowUpListDto> UpdateFollowUpAsync(int id, UpdateFollowUpDto dto, int companyId);
    Task<bool> DeleteFollowUpAsync(int id, int companyId);

    // ── Meetings ──────────────────────────────────────────────────────────
    Task<List<MeetingListDto>> ListMeetingsAsync(int? companyId, int? leadId = null, int? customerId = null);
    Task<PagedResult<MeetingListDto>> ListMeetingsPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc");
    Task<MeetingDetailDto?> GetMeetingAsync(int id, int? companyId);
    Task<MeetingListDto> CreateMeetingAsync(CreateMeetingDto dto, int companyId, int userId);
    Task<MeetingListDto> UpdateMeetingAsync(int id, UpdateMeetingDto dto, int companyId);
    Task<bool> DeleteMeetingAsync(int id, int companyId);

    // ── Field Visits ──────────────────────────────────────────────────────
    Task<List<VisitListDto>> ListVisitsAsync(int? companyId, int? leadId = null, int? customerId = null);
    Task<PagedResult<VisitListDto>> ListVisitsPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc");
    Task<VisitListDto> CheckInAsync(CheckInDto dto, int companyId, int userId);
    Task<bool> CheckOutAsync(int id, CheckOutDto dto, int companyId);
    Task<bool> DeleteVisitAsync(int id, int companyId);

    // ── Tasks ─────────────────────────────────────────────────────────────
    Task<List<SalesTaskListDto>> ListTasksAsync(
        int? companyId, int? leadId = null, int? customerId = null, string? status = null);
    Task<PagedResult<SalesTaskListDto>> ListTasksPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null, string? status = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc");
    Task<SalesTaskListDto> CreateTaskAsync(CreateSalesTaskDto dto, int companyId, int userId);
    Task<SalesTaskListDto> UpdateTaskAsync(int id, UpdateSalesTaskDto dto, int companyId);
    Task<bool> UpdateTaskStatusAsync(int id, string status, int companyId);
    Task<bool> DeleteTaskAsync(int id, int companyId);

    // ── Quotations ────────────────────────────────────────────────────────
    Task<List<QuotationListDto>> ListQuotationsAsync(
        int? companyId, int? leadId = null, int? customerId = null);
    Task<PagedResult<QuotationListDto>> ListQuotationsPagedAsync(
        int? companyId, int? leadId = null, int? customerId = null,
        int page = 1, int pageSize = 25, string? search = null,
        string? sortBy = null, string? sortDirection = "desc");
    Task<QuotationListDto?> GetQuotationAsync(int id, int? companyId);
    Task<QuotationListDto> CreateQuotationAsync(CreateQuotationDto dto, int companyId, int userId);
    Task<QuotationListDto> UpdateQuotationAsync(int id, UpdateQuotationDto dto, int companyId);
    Task<bool> UpdateQuotationStatusAsync(int id, string status, int companyId);
    Task<bool> DeleteQuotationAsync(int id, int companyId);

    // ── Reports ───────────────────────────────────────────────────────────
    Task<object> GetLeadReportAsync(int? companyId, DateTime? from, DateTime? to);
    Task<object> GetConversionReportAsync(int? companyId, DateTime? from, DateTime? to);
    Task<object> GetPerformanceReportAsync(int? companyId, DateTime? from, DateTime? to);
    Task<object> GetVisitReportAsync(int? companyId, DateTime? from, DateTime? to);
    Task<object> GetRevenueReportAsync(int? companyId, DateTime? from, DateTime? to);
    Task<object> GetPipelineReportAsync(int? companyId);
}
