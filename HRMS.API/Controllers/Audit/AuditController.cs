using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Audit;

/// <summary>
/// Read-only audit log — visible to superadmin only.
/// Admins can query their own user ID's events via /api/audit?userId={id}.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AuditController : BaseController
{
    private readonly IAuditService _audit;
    public AuditController(IAuditService audit) => _audit = audit;

    /// <summary>List recent audit events with optional filters</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? action,
        [FromQuery] int? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize is < 1 or > 200) pageSize = 50;
        var logs = await _audit.GetRecentAsync(page, pageSize, action, userId);
        return Ok(ApiResponse<List<AuditLog>>.Ok(logs));
    }
}
