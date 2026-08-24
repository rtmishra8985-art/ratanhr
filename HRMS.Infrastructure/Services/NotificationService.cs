using HRMS.Application.Common;
using HRMS.Application.DTOs.Notification;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    public NotificationService(ApplicationDbContext db) => _db = db;

    private async Task<int?> GetRecipientCompanyIdAsync(int userId)
        => await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync();

    public async Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        var q = _db.Notifications.Where(n => n.UserId == userId && n.CompanyId == companyId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        var list = await q.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
        return list.Select(Map).ToList();
    }

    // FIX 5: Accept sortBy / sortDirection and apply safe column-level ordering.
    // Allowed columns are whitelisted to prevent SQL injection.
    // Default: CreatedAt descending (most recent first).
    public async Task<PagedResult<NotificationDto>> GetForUserPagedAsync(
        int     userId,
        bool    unreadOnly,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "desc",
        string? type          = null,
        string? search        = null)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        var q = _db.Notifications.Where(n => n.UserId == userId && n.CompanyId == companyId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);

        // FIX: apply type and search filters in the DB query so CountAsync() and
        // Skip/Take operate on the already-filtered set — making TotalCount and
        // multi-page pagination correct for filtered requests.
        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(n => n.Type == type.Trim());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q2 = search.Trim().ToLower();
            q = q.Where(n => n.Title.ToLower().Contains(q2) || n.Message.ToLower().Contains(q2));
        }

        // Whitelist of sortable columns to prevent SQL injection.
        var allowedSort = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CreatedAt", "Title", "Type", "IsRead" };

        q = q.ApplySortingByDate(sortBy, sortDirection,
            defaultSelector:  n => n.CreatedAt,
            allowedColumns:   allowedSort);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var total = await q.CountAsync();
        var rows  = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return PagedResult<NotificationDto>.Create(rows.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        return await _db.Notifications.CountAsync(
            n => n.UserId == userId && n.CompanyId == companyId && !n.IsRead);
    }

    public async Task<bool> MarkReadAsync(int notificationId, int userId)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        var n = await _db.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId && x.CompanyId == companyId);
        if (n == null) return false;
        if (!n.IsRead) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return true;
    }

    public async Task<bool> MarkAllReadAsync(int userId)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.CompanyId == companyId && !n.IsRead)
            .ToListAsync();
        if (notifications.Count == 0) return true;
        var now = DateTime.UtcNow;
        foreach (var n in notifications) { n.IsRead = true; n.ReadAt = now; }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int notificationId, int userId)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        var n = await _db.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId && x.CompanyId == companyId);
        if (n == null) return false;
        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> CreateAsync(CreateNotificationDto dto)
    {
        var companyId = await GetRecipientCompanyIdAsync(dto.UserId);
        var n = new Notification
        {
            UserId     = dto.UserId,
            CompanyId  = companyId,
            Title      = dto.Title,
            Message    = dto.Message,
            Type       = dto.Type,
            EntityType = dto.EntityType,
            EntityId   = dto.EntityId,
            CreatedAt  = DateTime.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();
        return n.Id;
    }

    public async Task NotifyAsync(int userId, string title, string message, string type = "info",
                                  string? entityType = null, string? entityId = null)
    {
        var companyId = await GetRecipientCompanyIdAsync(userId);
        var n = new Notification
        {
            UserId     = userId,
            CompanyId  = companyId,
            Title      = title,
            Message    = message,
            Type       = type,
            EntityType = entityType,
            EntityId   = entityId,
            CreatedAt  = DateTime.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();
    }

    private static NotificationDto Map(Notification n) => new()
    {
        Id         = n.Id,
        Title      = n.Title,
        Message    = n.Message,
        Type       = n.Type,
        EntityType = n.EntityType,
        EntityId   = n.EntityId,
        IsRead     = n.IsRead,
        CreatedAt  = n.CreatedAt,
        ReadAt     = n.ReadAt
    };
}
