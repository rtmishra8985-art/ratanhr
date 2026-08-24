using HRMS.Application.Common;
using HRMS.Application.DTOs.Notification;

namespace HRMS.Application.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false);

    // FIX: Added type / search so filters are applied in the DB query before
    // paging, ensuring TotalCount and pagination are accurate across all pages.
    Task<PagedResult<NotificationDto>> GetForUserPagedAsync(
        int     userId,
        bool    unreadOnly,
        int     page,
        int     pageSize,
        string? sortBy        = null,
        string? sortDirection = "desc",
        string? type          = null,
        string? search        = null);

    Task<int>  GetUnreadCountAsync(int userId);
    Task<bool> MarkReadAsync(int notificationId, int userId);
    Task<bool> MarkAllReadAsync(int userId);
    Task<bool> DeleteAsync(int notificationId, int userId);
    Task<int>  CreateAsync(CreateNotificationDto dto);
    Task NotifyAsync(int userId, string title, string message, string type = "info",
                     string? entityType = null, string? entityId = null);
}
