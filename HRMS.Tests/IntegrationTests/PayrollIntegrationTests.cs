using HRMS.Application.DTOs.Payroll;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests.IntegrationTests;

/// <summary>
/// Phase 1 – Integration tests: Payroll module with PayrollLockGuard.
/// Verifies that bulk payroll generation interacts correctly with the lock state.
/// </summary>
public class PayrollIntegrationTests
{
    [Fact]
    public async Task GenerateThenLock_SubsequentBulkIsBlockedByGuard()
    {
        using var db = TestHelpers.CreateInMemoryDb();

        db.Employees.Add(new Employee { EmployeeCode = "EMP_INT1", FullName = "Integration Test",
            IsActive = true, CompanyId = 1 });
        db.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeId = "EMP_INT1", BasicPay = 25000, HRA = 10000, IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var payrollSvc = new PayrollService(db, new MockAuditService(), new MockNotificationService(),
                                            new MockPayrollCalculator(), new MockLogger<PayrollService>());
        var guard      = new PayrollLockGuard(db);

        // Step 1: Generate payroll for July 2026
        var result = await payrollSvc.BulkGeneratePayslipsAsync(
            new BulkPayrollDto { Month = 7, Year = 2026, CompanyId = 1 });
        Assert.Equal(1, result.Generated);

        // Step 2: Lock the period
        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);

        // Step 3: Guard now reports the period as locked
        var isLocked = await guard.IsLockedAsync(1, 7, 2026);
        Assert.True(isLocked);

        // Step 4: A controller would return 409 based on GetLockMessageAsync
        var lockMsg = await guard.GetLockMessageAsync(1, 7, 2026);
        Assert.NotNull(lockMsg);
        Assert.Contains("locked", lockMsg!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnlockPeriod_AllowsNewGeneration()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);
        await guard.UnlockAsync(1, 7, 2026, 1);

        var msg = await guard.GetLockMessageAsync(1, 7, 2026);
        Assert.Null(msg); // Open — generation allowed
    }
}
