using HRMS.Application.DTOs.Sales;
using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRMS.API.Security;

namespace HRMS.API.Controllers.Sales;

[ApiController]
[Route("api/sales")]
[Authorize(Roles = AppRoles.AdminSuperAdminSales)]
[RequireTenantForWrite]
public class SalesController : BaseController
{
    private readonly ISalesService _svc;

    // GET/report operations may use null for SuperAdmin cross-company reads.
    // Write operations are guarded by RequireTenantForWrite because their service
    // contracts require a concrete company ID.
    private int? CallerCompanyId => CallerCompanyIdOrNull;
    private int ActorUserId     => UserId;

    public SalesController(ISalesService svc) => _svc = svc;

    // ── Dashboard ──────────────────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var data = await _svc.GetSalesDashboardAsync(CallerCompanyId);
        return Ok(ApiResponse<object>.Ok(data));
    }

    // ── Leads ──────────────────────────────────────────────────────────────
    [HttpGet("leads")]
    public async Task<IActionResult> ListLeads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var (items, total) = await _svc.ListLeadsAsync(CallerCompanyId, page, pageSize, status, search);
        return Ok(ApiResponse<object>.Ok(new { data = items, total, page, pageSize }));
    }

    [HttpGet("leads/{id:int}")]
    public async Task<IActionResult> GetLead(int id)
    {
        var data = await _svc.GetLeadAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Lead not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("leads")]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        try
        {
            var result = await _svc.CreateLeadAsync(dto, CallerCompanyId ?? 0, ActorUserId);
            return Ok(ApiResponse<object>.Ok(result, "Lead created."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("leads/{id:int}")]
    public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        try
        {
            var result = await _svc.UpdateLeadAsync(id, dto, CallerCompanyId ?? 0);
            return Ok(ApiResponse<object>.Ok(result, "Lead updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Lead not found.")); }
    }

    [HttpPatch("leads/{id:int}/status")]
    public async Task<IActionResult> UpdateLeadStatus(int id, [FromBody] UpdateLeadStatusDto dto)
    {
        var ok = await _svc.UpdateLeadStatusAsync(id, dto.Status, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Lead status updated.")) : NotFound(ApiResponse.Fail("Lead not found."));
    }

    [HttpDelete("leads/{id:int}")]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var ok = await _svc.DeleteLeadAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Lead deleted.")) : NotFound(ApiResponse.Fail("Lead not found."));
    }

    // ── Customers ──────────────────────────────────────────────────────────
    [HttpGet("customers")]
    public async Task<IActionResult> ListCustomers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var (items, total) = await _svc.ListCustomersAsync(CallerCompanyId, page, pageSize, search);
        return Ok(ApiResponse<object>.Ok(new { data = items, total, page, pageSize }));
    }

    [HttpGet("customers/{id:int}")]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var data = await _svc.GetCustomerAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Customer not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CreateCustomerAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(result, "Customer created."));
    }

    [HttpPost("leads/{leadId:int}/convert")]
    public async Task<IActionResult> ConvertLeadToCustomer(int leadId, [FromBody] CreateCustomerDto dto)
    {
        try
        {
            var result = await _svc.ConvertLeadToCustomerAsync(leadId, dto, CallerCompanyId ?? 0, ActorUserId);
            return Ok(ApiResponse<object>.Ok(result, "Lead converted to customer."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Lead not found.")); }
    }

    [HttpPut("customers/{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
    {
        try
        {
            var result = await _svc.UpdateCustomerAsync(id, dto, CallerCompanyId ?? 0);
            return Ok(ApiResponse<object>.Ok(result, "Customer updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Customer not found.")); }
    }

    [HttpDelete("customers/{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var ok = await _svc.DeleteCustomerAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Customer deleted.")) : NotFound(ApiResponse.Fail("Customer not found."));
    }

    // ── Follow-Ups ─────────────────────────────────────────────────────────
    [HttpGet("followups")]
    public async Task<IActionResult> ListFollowUps(
        [FromQuery] int? leadId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var data = await _svc.ListFollowUpsPagedAsync(
            CallerCompanyId, leadId, status, page, pageSize, search, sortBy, sortDirection);
        return Ok(ApiResponse<PagedResult<FollowUpListDto>>.Ok(data));
    }

    [HttpPost("followups")]
    public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CreateFollowUpAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(result, "Follow-up scheduled."));
    }

    [HttpPut("followups/{id:int}")]
    public async Task<IActionResult> UpdateFollowUp(int id, [FromBody] UpdateFollowUpDto dto)
    {
        try
        {
            var result = await _svc.UpdateFollowUpAsync(id, dto, CallerCompanyId ?? 0);
            return Ok(ApiResponse<object>.Ok(result, "Follow-up updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Follow-up not found.")); }
    }

    [HttpDelete("followups/{id:int}")]
    public async Task<IActionResult> DeleteFollowUp(int id)
    {
        var ok = await _svc.DeleteFollowUpAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Follow-up deleted.")) : NotFound(ApiResponse.Fail("Follow-up not found."));
    }

    // ── Meetings ───────────────────────────────────────────────────────────
    [HttpGet("meetings")]
    public async Task<IActionResult> ListMeetings(
        [FromQuery] int? leadId, [FromQuery] int? customerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var data = await _svc.ListMeetingsPagedAsync(
            CallerCompanyId, leadId, customerId, page, pageSize, search, sortBy, sortDirection);
        return Ok(ApiResponse<PagedResult<MeetingListDto>>.Ok(data));
    }

    [HttpGet("meetings/{id:int}")]
    public async Task<IActionResult> GetMeeting(int id)
    {
        var data = await _svc.GetMeetingAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Meeting not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("meetings")]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CreateMeetingAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(result, "Meeting scheduled."));
    }

    [HttpPut("meetings/{id:int}")]
    public async Task<IActionResult> UpdateMeeting(int id, [FromBody] UpdateMeetingDto dto)
    {
        try
        {
            var result = await _svc.UpdateMeetingAsync(id, dto, CallerCompanyId ?? 0);
            return Ok(ApiResponse<object>.Ok(result, "Meeting updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Meeting not found.")); }
    }

    [HttpDelete("meetings/{id:int}")]
    public async Task<IActionResult> DeleteMeeting(int id)
    {
        var ok = await _svc.DeleteMeetingAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Meeting deleted.")) : NotFound(ApiResponse.Fail("Meeting not found."));
    }

    // ── Field Visits ───────────────────────────────────────────────────────
    [HttpGet("visits")]
    public async Task<IActionResult> ListVisits(
        [FromQuery] int? leadId, [FromQuery] int? customerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var data = await _svc.ListVisitsPagedAsync(
            CallerCompanyId, leadId, customerId, page, pageSize, search, sortBy, sortDirection);
        return Ok(ApiResponse<PagedResult<VisitListDto>>.Ok(data));
    }

    [HttpPost("visits/checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CheckInAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(result, "Checked in."));
    }

    [HttpPatch("visits/{id:int}/checkout")]
    public async Task<IActionResult> CheckOut(int id, [FromBody] CheckOutDto dto)
    {
        var ok = await _svc.CheckOutAsync(id, dto, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Checked out.")) : NotFound(ApiResponse.Fail("Visit not found."));
    }

    [HttpDelete("visits/{id:int}")]
    public async Task<IActionResult> DeleteVisit(int id)
    {
        var ok = await _svc.DeleteVisitAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Visit deleted.")) : NotFound(ApiResponse.Fail("Visit not found."));
    }

    // ── Tasks ──────────────────────────────────────────────────────────────
    [HttpGet("tasks")]
    public async Task<IActionResult> ListTasks(
        [FromQuery] int? leadId, [FromQuery] int? customerId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var data = await _svc.ListTasksPagedAsync(
            CallerCompanyId, leadId, customerId, status, page, pageSize, search, sortBy, sortDirection);
        return Ok(ApiResponse<PagedResult<SalesTaskListDto>>.Ok(data));
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateSalesTaskDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CreateTaskAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(result, "Task created."));
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateSalesTaskDto dto)
    {
        try
        {
            var result = await _svc.UpdateTaskAsync(id, dto, CallerCompanyId ?? 0);
            return Ok(ApiResponse<object>.Ok(result, "Task updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Task not found.")); }
    }

    [HttpPatch("tasks/{id:int}/status")]
    public async Task<IActionResult> UpdateTaskStatus(int id, [FromBody] UpdateTaskStatusDto dto)
    {
        var ok = await _svc.UpdateTaskStatusAsync(id, dto.Status, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Task status updated.")) : NotFound(ApiResponse.Fail("Task not found."));
    }

    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var ok = await _svc.DeleteTaskAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Task deleted.")) : NotFound(ApiResponse.Fail("Task not found."));
    }

    // ── Quotations ─────────────────────────────────────────────────────────
    [HttpGet("quotations")]
    public async Task<IActionResult> ListQuotations(
        [FromQuery] int? leadId, [FromQuery] int? customerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var data = await _svc.ListQuotationsPagedAsync(
            CallerCompanyId, leadId, customerId, page, pageSize, search, sortBy, sortDirection);
        return Ok(ApiResponse<PagedResult<QuotationListDto>>.Ok(data));
    }

    [HttpGet("quotations/{id:int}")]
    public async Task<IActionResult> GetQuotation(int id)
    {
        var data = await _svc.GetQuotationAsync(id, CallerCompanyId);
        if (data is null) return NotFound(ApiResponse.Fail("Quotation not found."));
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPost("quotations")]
    public async Task<IActionResult> CreateQuotation([FromBody] CreateQuotationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Fail("Invalid input."));
        var result = await _svc.CreateQuotationAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(result, "Quotation created."));
    }

    [HttpPut("quotations/{id:int}")]
    public async Task<IActionResult> UpdateQuotation(int id, [FromBody] UpdateQuotationDto dto)
    {
        try
        {
            var result = await _svc.UpdateQuotationAsync(id, dto, CallerCompanyId ?? 0);
            return Ok(ApiResponse<object>.Ok(result, "Quotation updated."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Quotation not found.")); }
    }

    [HttpPatch("quotations/{id:int}/status")]
    public async Task<IActionResult> UpdateQuotationStatus(int id, [FromBody] UpdateQuotationStatusDto dto)
    {
        var ok = await _svc.UpdateQuotationStatusAsync(id, dto.Status, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Quotation status updated.")) : NotFound(ApiResponse.Fail("Quotation not found."));
    }

    [HttpDelete("quotations/{id:int}")]
    public async Task<IActionResult> DeleteQuotation(int id)
    {
        var ok = await _svc.DeleteQuotationAsync(id, CallerCompanyId ?? 0);
        return ok ? Ok(ApiResponse.Ok("Quotation deleted.")) : NotFound(ApiResponse.Fail("Quotation not found."));
    }

    // ── Reports ────────────────────────────────────────────────────────────
    [HttpGet("reports/leads")]
    public async Task<IActionResult> LeadReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var data = await _svc.GetLeadReportAsync(CallerCompanyId, from, to);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("reports/conversion")]
    public async Task<IActionResult> ConversionReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var data = await _svc.GetConversionReportAsync(CallerCompanyId, from, to);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("reports/performance")]
    public async Task<IActionResult> PerformanceReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var data = await _svc.GetPerformanceReportAsync(CallerCompanyId, from, to);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("reports/visits")]
    public async Task<IActionResult> VisitReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var data = await _svc.GetVisitReportAsync(CallerCompanyId, from, to);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("reports/revenue")]
    public async Task<IActionResult> RevenueReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var data = await _svc.GetRevenueReportAsync(CallerCompanyId, from, to);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpGet("reports/pipeline")]
    public async Task<IActionResult> PipelineReport()
    {
        var data = await _svc.GetPipelineReportAsync(CallerCompanyId);
        return Ok(ApiResponse<object>.Ok(data));
    }

// ── Lead Assignment ────────────────────────────────────────────────────────

    [HttpPost("leads/{id:int}/assign")]
    [Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]
    public async Task<IActionResult> AssignLead(int id, [FromBody] AssignLeadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AssignedToEmployeeId))
            return BadRequest(ApiResponse.Fail("AssignedToEmployeeId is required."));
        try
        {
            var result = await _svc.AssignLeadAsync(id, dto, CallerCompanyId ?? 0, ActorUserId);
            return Ok(ApiResponse<object>.Ok(result, "Lead assigned."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Lead not found.")); }
    }

    [HttpPost("leads/{id:int}/reassign")]
    [Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]
    public async Task<IActionResult> ReassignLead(int id, [FromBody] ReassignLeadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewAssignedToEmployeeId))
            return BadRequest(ApiResponse.Fail("NewAssignedToEmployeeId is required."));
        try
        {
            var result = await _svc.ReassignLeadAsync(id, dto, CallerCompanyId ?? 0, ActorUserId);
            return Ok(ApiResponse<object>.Ok(result, "Lead reassigned."));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Lead not found.")); }
    }

    [HttpPost("leads/bulk-assign")]
    [Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]
    public async Task<IActionResult> BulkAssignLeads([FromBody] BulkAssignLeadsDto dto)
    {
        if (dto.LeadIds == null || dto.LeadIds.Count == 0)
            return BadRequest(ApiResponse.Fail("No lead IDs provided."));
        if (string.IsNullOrWhiteSpace(dto.AssignedToEmployeeId))
            return BadRequest(ApiResponse.Fail("AssignedToEmployeeId is required."));

        var count = await _svc.BulkAssignLeadsAsync(dto, CallerCompanyId ?? 0, ActorUserId);
        return Ok(ApiResponse<object>.Ok(new { count }, $"{count} lead(s) assigned."));
    }

    [HttpGet("leads/{id:int}/assignment-history")]
    public async Task<IActionResult> GetAssignmentHistory(int id)
    {
        try
        {
            var history = await _svc.GetLeadAssignmentHistoryAsync(id, CallerCompanyId);
            return Ok(ApiResponse<object>.Ok(history));
        }
        catch (KeyNotFoundException) { return NotFound(ApiResponse.Fail("Lead not found.")); }
    }

    [HttpGet("leads/my-leads")]
    public async Task<IActionResult> MyAssignedLeads(
        [FromQuery] string employeeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(ApiResponse.Fail("employeeId is required."));
        var (items, total) = await _svc.GetMyAssignedLeadsAsync(employeeId, CallerCompanyId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(new { data = items, total, page, pageSize }));
    }

    [HttpGet("leads/unassigned")]
    [Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]
    public async Task<IActionResult> UnassignedLeads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _svc.GetUnassignedLeadsAsync(CallerCompanyId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(new { data = items, total, page, pageSize }));
    }

    [HttpGet("leads/team-leads")]
    [Authorize(Roles = AppRoles.AdminSuperAdminSalesManagers)]
    public async Task<IActionResult> TeamLeads(
        [FromQuery] string managerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(managerId))
            return BadRequest(ApiResponse.Fail("managerId is required."));
        var (items, total) = await _svc.GetTeamLeadsAsync(managerId, CallerCompanyId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(new { data = items, total, page, pageSize }));
    }
}
