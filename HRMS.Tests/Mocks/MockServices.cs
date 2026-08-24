using HRMS.Application.DTOs.Payroll;
using HRMS.Application.Interfaces;
using HRMS.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HRMS.Tests.Mocks;

/// <summary>No-op audit service for unit tests.</summary>
public class MockAuditService : IAuditService
{
    // Parameter names and order must match IAuditService exactly.
    public Task LogAsync(string action, string entityType, string? entityId = null,
        int? actorId = null, string? actorName = null,
        int? companyId = null,
        string? details = null, bool success = true,
        string? ipAddress = null) => Task.CompletedTask;

    public Task<List<HRMS.Domain.Entities.AuditLog>> GetLogsAsync(
        int companyId, string? entityType = null, int? actorId = null, CancellationToken ct = default) =>
        Task.FromResult(new List<HRMS.Domain.Entities.AuditLog>());

    public Task<List<HRMS.Domain.Entities.AuditLog>> GetRecentAsync(int page = 1, int pageSize = 50,
        string? action = null, int? userId = null) =>
        Task.FromResult(new List<HRMS.Domain.Entities.AuditLog>());
}

/// <summary>No-op email service for unit tests.</summary>
public class MockEmailService : IEmailService
{
    public Task SendPasswordResetAsync(string toEmail, string toName, string resetLink) => Task.CompletedTask;
    public Task SendWelcomeAsync(string toEmail, string toName, string employeeId, string tempPassword) => Task.CompletedTask;
    public Task SendLeaveDecisionAsync(string toEmail, string toName, string leaveType,
        string fromDate, string toDate, bool approved, string? remarks) => Task.CompletedTask;
    public Task SendAsync(string toEmail, string subject, string htmlBody) => Task.CompletedTask;
}

/// <summary>No-op logger for unit tests.</summary>
public class MockLogger<T> : ILogger<T>
{
    IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) { }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

/// <summary>No-op notification service for unit tests.</summary>
public class MockNotificationService : INotificationService
{
    public Task<List<HRMS.Application.DTOs.Notification.NotificationDto>> GetForUserAsync(int userId, bool unreadOnly = false)
        => Task.FromResult(new List<HRMS.Application.DTOs.Notification.NotificationDto>());

    public Task<HRMS.Application.Common.PagedResult<HRMS.Application.DTOs.Notification.NotificationDto>> GetForUserPagedAsync(
        int userId, bool unreadOnly, int page, int pageSize,
        string? sortBy = null, string? sortDirection = "desc",
        string? type = null, string? search = null)
        => Task.FromResult(HRMS.Application.Common.PagedResult<HRMS.Application.DTOs.Notification.NotificationDto>
            .Create(new(), 0, page, pageSize));

    public Task<int>  GetUnreadCountAsync(int userId)                  => Task.FromResult(0);
    public Task<bool> MarkReadAsync(int notificationId, int userId)    => Task.FromResult(true);
    public Task<bool> MarkAllReadAsync(int userId)                     => Task.FromResult(true);
    public Task<bool> DeleteAsync(int notificationId, int userId)      => Task.FromResult(true);
    public Task<int>  CreateAsync(HRMS.Application.DTOs.Notification.CreateNotificationDto dto) => Task.FromResult(0);
    public Task NotifyAsync(int userId, string title, string message, string type = "info",
                            string? entityType = null, string? entityId = null) => Task.CompletedTask;
}

/// <summary>
/// No-op PayrollLockGuard — all periods are open.
/// Use <see cref="MockLockedPayrollLockGuard"/> to simulate a locked period.
/// </summary>
public class MockPayrollLockGuard : IPayrollLockGuard
{
    public Task<bool>   IsLockedAsync(int companyId, int month, int year)          => Task.FromResult(false);
    public Task<string?> GetLockMessageAsync(int companyId, int month, int year)   => Task.FromResult<string?>(null);
    public Task LockAsync(int companyId, int month, int year, int lockedByUserId, string? notes = null)   => Task.CompletedTask;
    public Task UnlockAsync(int companyId, int month, int year, int unlockedByUserId, string? notes = null) => Task.CompletedTask;
    public Task<List<PayrollLockDto>> GetLocksAsync(int? companyId, int? year = null)
        => Task.FromResult(new List<PayrollLockDto>());
}

/// <summary>Simulates a fully-locked payroll period (every period is locked).</summary>
public class MockLockedPayrollLockGuard : IPayrollLockGuard
{
    public Task<bool> IsLockedAsync(int companyId, int month, int year) => Task.FromResult(true);
    public Task<string?> GetLockMessageAsync(int companyId, int month, int year)
        => Task.FromResult<string?>($"Payroll period {month:00}/{year} for company {companyId} is locked.");
    public Task LockAsync(int companyId, int month, int year, int lockedByUserId, string? notes = null)   => Task.CompletedTask;
    public Task UnlockAsync(int companyId, int month, int year, int unlockedByUserId, string? notes = null) => Task.CompletedTask;
    public Task<List<PayrollLockDto>> GetLocksAsync(int? companyId, int? year = null)
        => Task.FromResult(new List<PayrollLockDto>());
}

/// <summary>
/// Passthrough payroll calculator for unit tests.
/// Returns pro-rated basic + 40% HRA with zero statutory deductions,
/// so tests can assert on net pay without relying on Indian tax rules.
/// </summary>
/// <summary>
/// Payroll calculator test double. Delegates to the production
/// <see cref="HRMS.Infrastructure.Payroll.IndianPayrollCalculator"/> so unit tests
/// exercise the real statutory rules (PF ceiling, ESI, PT slabs, TDS) instead of a
/// simplified stub that silently reported zero deductions.
/// It is fully deterministic and has no external dependencies.
/// </summary>
public class MockPayrollCalculator : IPayrollCalculator
{
    private readonly IPayrollCalculator _inner = new HRMS.Infrastructure.Payroll.IndianPayrollCalculator();

    public string Jurisdiction => "Test";

    public PayrollCalculationResult Calculate(PayrollCalculationRequest request)
        => _inner.Calculate(request);
}


/// <summary>No-op cache service for unit tests — always invokes the factory, never caches.</summary>
public class MockCacheService : ICacheService
{
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? ttl = null, CancellationToken ct = default)
        => factory();

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>No-op file storage service for unit tests — always returns null paths, never touches disk.</summary>
public class MockFileStorageService : IFileStorageService
{
    public Task<string?> SaveAsync(IFormFile? file, string subfolder)       => Task.FromResult<string?>(null);
    public Task<string?> SaveAsync(IFormFile? file, string subfolder, HRMS.Infrastructure.Security.UploadProfile? profile)
        => Task.FromResult<string?>(null);
    public Task<string?> SaveFileAsync(IFormFile? file, string subfolder)   => Task.FromResult<string?>(null);
    public Task<string?> SaveFileAsync(IFormFile? file, string subfolder, HRMS.Infrastructure.Security.UploadProfile? profile)
        => Task.FromResult<string?>(null);
    public Task<Stream> RetrieveAsync(string relativePath)                  => Task.FromResult<Stream>(Stream.Null);
    public void Delete(string? relativePath) { }
}
