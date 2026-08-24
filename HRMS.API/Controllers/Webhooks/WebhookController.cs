using HRMS.Application.Common;
using HRMS.Application.DTOs.Webhook;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Webhooks;

[ApiController]
[Route("api/webhooks")]
[Authorize(Roles = AppRoles.AdminAndSuperAdmin)]
public class WebhookController : BaseController
{
    private readonly IWebhookService _service;
    public WebhookController(IWebhookService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        // FIX: was CompanyId (int, -1 sentinel for SuperAdmin) — IWebhookService.ListAsync
        // takes int? and treats null as "unrestricted", so SuperAdmin calls were silently
        // scoped to the impossible company_id = -1 and always returned an empty list.
        // Delete() below already used CallerCompanyIdOrNull; this brings List/Register in line.
        var result = await _service.ListAsync(CallerCompanyIdOrNull);
        return Ok(ApiResponse<List<WebhookDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] CreateWebhookDto dto)
    {
        try
        {
            // FIX: was CompanyId (int, -1 sentinel for SuperAdmin) — see note on List() above.
            var result = await _service.RegisterAsync(CallerCompanyIdOrNull, dto);
            // FIX: HTTP 201 Created for resource creation (was 200 OK).
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<WebhookDto>.Ok(result, "Webhook registered."));
        }
        catch (ArgumentException ex)
        {
            // FIX SSRF: RegisterAsync throws ArgumentException when TargetUrl fails
            // the SSRF allowlist check. Return 400 so the caller knows it was a bad URL,
            // not a server error.
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        // FIX AUDIT-07S-02: pass the nullable caller scope (null only for SuperAdmin) so the
        // service can distinguish 'unrestricted' from 'company 0/-1'.
        var ok = await _service.DeleteAsync(id, CallerCompanyIdOrNull);
        return ok ? Ok(ApiResponse.Ok("Webhook removed."))
                  : NotFound(ApiResponse.Fail("Subscription not found."));
    }

    /// <summary>
    /// Returns the list of all supported webhook event types that can be subscribed to.
    /// Fixes gap: callers had no discovery endpoint to know which EventType strings are valid.
    /// </summary>
    [HttpGet("events")]
    public IActionResult GetEventTypes()
    {
        var events = new[]
        {
            // Employee lifecycle
            "employee.created",
            "employee.updated",
            "employee.deactivated",
            "employee.exit",

            // Leave
            "leave.requested",
            "leave.approved",
            "leave.rejected",
            "leave.cancelled",

            // Attendance
            "attendance.marked",
            "attendance.regularised",

            // Payroll
            "payroll.processed",
            "payroll.locked",
            "payslip.generated",

            // Recruitment
            "candidate.applied",
            "candidate.shortlisted",
            "candidate.offered",
            "candidate.hired",
            "candidate.rejected",

            // Performance
            "performance.review.submitted",
            "performance.goal.completed",

            // Helpdesk
            "ticket.created",
            "ticket.resolved",
            "ticket.closed",

            // Onboarding
            "onboarding.assigned",
            "onboarding.completed",

            // Expense / Travel
            "expense.submitted",
            "expense.approved",
            "expense.rejected",
            "travel.requested",
            "travel.approved",

            // Assets
            "asset.assigned",
            "asset.returned",

            // Training
            "training.assigned",
            "training.completed",

            // Sales / CRM
            "sales.lead.created",
            "sales.lead.converted",
            "sales.quotation.accepted",
            "sales.quotation.rejected",
        };

        return Ok(ApiResponse<string[]>.Ok(events, $"{events.Length} event types supported."));
    }
}
