using HRMS.Application.Common;
using HRMS.Application.DTOs.Notification;
using HRMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.API.Controllers.Notifications;

/// <summary>
/// FIX 2 + FIX 5: In-app notification centre for the authenticated user.
/// Full implementation: list (paged + filtered + sorted), unread count,
/// mark-read, mark-all-read, delete, search, and filters.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Policy = "RequireMfaCompleted")]
public class NotificationController : BaseController
{
    private readonly INotificationService _svc;
    public NotificationController(INotificationService svc) => _svc = svc;

    /// <summary>
    /// Get all notifications for the current user.
    /// Supports pagination, unread filter, free-text search (title/message),
    /// type filter, and column sorting.
    /// </summary>
    /// <param name="unreadOnly">When true, return only unread notifications.</param>
    /// <param name="type">Optional filter: info | warning | error | success</param>
    /// <param name="search">Free-text search on title and message.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page (1–100, default 25).</param>
    /// <param name="sortBy">Column to sort by (CreatedAt | Title | Type | IsRead).</param>
    /// <param name="sortDirection">asc or desc (default desc = newest first).</param>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool    unreadOnly    = false,
        [FromQuery] string? type          = null,
        [FromQuery] string? search        = null,
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 25,
        [FromQuery] string? sortBy        = null,
        [FromQuery] string? sortDirection = "desc")
    {
        // FIX: type and search are now pushed into the DB query via the service so
        // TotalCount, Skip, and Take all operate on the already-filtered result set.
        // Pagination is therefore correct even when a filter spans multiple pages.
        var result = await _svc.GetForUserPagedAsync(
            UserId, unreadOnly, page, pageSize, sortBy, sortDirection,
            type: type, search: search);

        return Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(result));
    }

    /// <summary>Unread notification count badge.</summary>
    [HttpGet("count")]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await _svc.GetUnreadCountAsync(UserId);
        return Ok(ApiResponse<object>.Ok(new { UnreadCount = count }));
    }

    /// <summary>Mark a single notification as read.</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var ok = await _svc.MarkReadAsync(id, UserId);
        return ok
            ? Ok(ApiResponse.Ok("Marked as read."))
            : NotFound(ApiResponse.Fail("Notification not found."));
    }

    /// <summary>Mark all notifications as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _svc.MarkAllReadAsync(UserId);
        return Ok(ApiResponse.Ok("All notifications marked as read."));
    }

    /// <summary>Delete a single notification.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteAsync(id, UserId);
        return ok
            ? Ok(ApiResponse.Ok("Notification deleted."))
            : NotFound(ApiResponse.Fail("Notification not found."));
    }
}
