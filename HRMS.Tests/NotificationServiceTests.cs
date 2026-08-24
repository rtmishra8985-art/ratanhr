using HRMS.Application.DTOs.Notification;
using HRMS.Infrastructure.Services;
using Xunit;

namespace HRMS.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateAndGetNotifications()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Test", Message = "Hello" });
        await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Test2", Message = "World", Type = "success" });

        var list = await svc.GetForUserAsync(userId: 1);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetUnreadCount_AccurateAfterMarkRead()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        var id = await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "A", Message = "B" });
        Assert.Equal(1, await svc.GetUnreadCountAsync(1));

        await svc.MarkReadAsync(id, userId: 1);
        Assert.Equal(0, await svc.GetUnreadCountAsync(1));
    }

    [Fact]
    public async Task MarkAllRead_MarksAllForUser()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        for (int i = 0; i < 5; i++)
            await svc.CreateAsync(new CreateNotificationDto { UserId = 2, Title = $"N{i}", Message = "msg" });

        Assert.Equal(5, await svc.GetUnreadCountAsync(2));
        await svc.MarkAllReadAsync(2);
        Assert.Equal(0, await svc.GetUnreadCountAsync(2));
    }

    [Fact]
    public async Task UnreadOnly_Filter_Works()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        var id = await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Read", Message = "x" });
        await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Unread", Message = "y" });
        await svc.MarkReadAsync(id, 1);

        var unread = await svc.GetForUserAsync(1, unreadOnly: true);
        Assert.Single(unread);
        Assert.Equal("Unread", unread[0].Title);
    }

    [Fact]
    public async Task DeleteNotification_RemovesIt()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        var id = await svc.CreateAsync(new CreateNotificationDto { UserId = 1, Title = "Del", Message = "x" });
        var ok = await svc.DeleteAsync(id, userId: 1);
        Assert.True(ok);

        var list = await svc.GetForUserAsync(1);
        Assert.Empty(list);
    }

    // ── Paged + filtered: TotalCount and page slicing must operate on the
    //    DB-filtered set, not on the unfiltered total.  Pre-fix, TotalCount
    //    was always the unfiltered count — these tests would have failed. ──

    [Fact]
    public async Task GetForUserPagedAsync_TypeFilter_TotalCountMatchesOnlyFilteredRows()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        // 4 "info", 2 "warning" for user 10
        for (int i = 0; i < 4; i++)
            await svc.CreateAsync(new CreateNotificationDto
                { UserId = 10, Title = $"Info {i}", Message = "m", Type = "info" });
        for (int i = 0; i < 2; i++)
            await svc.CreateAsync(new CreateNotificationDto
                { UserId = 10, Title = $"Warn {i}", Message = "m", Type = "warning" });

        var result = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 1, pageSize: 25, type: "warning");

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, n => Assert.Equal("warning", n.Type));
    }

    [Fact]
    public async Task GetForUserPagedAsync_SearchFilter_TotalCountMatchesOnlyFilteredRows()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Payslip ready", Message = "Your payslip is available." });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Leave approved", Message = "Your leave request was approved." });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "System alert", Message = "Maintenance tonight." });

        var result = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 1, pageSize: 25, search: "payslip");

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Contains("Payslip", result.Items[0].Title,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetForUserPagedAsync_SearchInMessage_AlsoMatches()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Generic title", Message = "Your salary has been credited." });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Other", Message = "Nothing special here." });

        var result = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 1, pageSize: 25, search: "salary");

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetForUserPagedAsync_SecondPage_ContainsCorrectItems()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        // Create 5 "error" notifications
        for (int i = 1; i <= 5; i++)
            await svc.CreateAsync(new CreateNotificationDto
                { UserId = 10, Title = $"Error {i}", Message = "m", Type = "error" });
        // Create 3 "info" (should not appear when filtering by "error")
        for (int i = 1; i <= 3; i++)
            await svc.CreateAsync(new CreateNotificationDto
                { UserId = 10, Title = $"Info {i}", Message = "m", Type = "info" });

        // Page 2 of error-filtered results, pageSize 3 → 2 items on page 2
        var page2 = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 2, pageSize: 3, type: "error");

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Items.Count);
        Assert.All(page2.Items, n => Assert.Equal("error", n.Type));
    }

    [Fact]
    public async Task GetForUserPagedAsync_TypeAndSearch_Combined_TotalCountCorrect()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Invoice overdue", Message = "Pay now", Type = "error" });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Invoice sent", Message = "FYI", Type = "info" });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "System error", Message = "Disk full", Type = "error" });

        // Filter: type=error AND search=invoice
        var result = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 1, pageSize: 25, type: "error", search: "invoice");

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("error", result.Items[0].Type);
        Assert.Contains("Invoice", result.Items[0].Title,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetForUserPagedAsync_EmptyTypeFilter_ReturnsAllTypes()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "A", Message = "m", Type = "info" });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "B", Message = "m", Type = "error" });

        var result = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 1, pageSize: 25, type: null);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetForUserPagedAsync_Filter_DoesNotLeakOtherUsersNotifications()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new NotificationService(db);

        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 10, Title = "Mine", Message = "m", Type = "info" });
        await svc.CreateAsync(new CreateNotificationDto
            { UserId = 99, Title = "NotMine", Message = "m", Type = "info" });

        var result = await svc.GetForUserPagedAsync(
            userId: 10, unreadOnly: false,
            page: 1, pageSize: 25, type: "info");

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, n =>
            Assert.DoesNotContain("NotMine", n.Title));
    }
}
