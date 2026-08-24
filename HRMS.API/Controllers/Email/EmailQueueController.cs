using HRMS.Application.Common;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers.Email;

[ApiController]
[Route("api/email-queue")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class EmailQueueController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailQueueService _svc;

    public EmailQueueController(ApplicationDbContext db, IEmailQueueService svc)
    {
        _db  = db;
        _svc = svc;
    }

    /// <summary>List email queue items filtered by status.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string status = "Pending",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.EmailQueue.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { Total = total, Page = page, Items = items },
            "Email queue retrieved."));
    }

    /// <summary>Manually retry a Failed email queue item.</summary>
    [HttpPost("{id:int}/retry")]
    public async Task<IActionResult> Retry(int id)
    {
        var item = await _db.EmailQueue.FindAsync(id);
        if (item == null)
            return NotFound(ApiResponse.Fail("Email queue item not found."));

        item.Status      = "Pending";
        item.RetryCount  = 0;
        item.NextRetryAt = null;
        item.LastError   = null;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Email queued for retry."));
    }
}
