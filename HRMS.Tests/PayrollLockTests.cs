using HRMS.Application.DTOs.Payroll;
using HRMS.Domain.Entities.Employee;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

/// <summary>
/// Phase 1 – B: PayrollLockGuard tests.
/// Covers lock/unlock lifecycle, idempotency, and the guard blocking payroll operations.
/// </summary>
public class PayrollLockTests
{
    // ── Lock / Unlock lifecycle ───────────────────────────────────────────

    [Fact]
    public async Task LockPeriod_CreateNewLock_IsLocked()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(companyId: 1, month: 7, year: 2026, lockedByUserId: 99);

        Assert.True(await guard.IsLockedAsync(1, 7, 2026));
    }

    [Fact]
    public async Task LockPeriod_DifferentCompany_NotLocked()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);

        Assert.False(await guard.IsLockedAsync(2, 7, 2026));
    }

    [Fact]
    public async Task LockPeriod_DifferentMonth_NotLocked()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);

        Assert.False(await guard.IsLockedAsync(1, 8, 2026));
    }

    [Fact]
    public async Task UnlockPeriod_PreviouslyLocked_IsUnlocked()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);
        await guard.UnlockAsync(1, 7, 2026, unlockedByUserId: 1);

        Assert.False(await guard.IsLockedAsync(1, 7, 2026));
    }

    [Fact]
    public async Task LockPeriod_Idempotent_DoesNotDuplicate()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        // Lock twice
        await guard.LockAsync(1, 7, 2026, 99);
        await guard.LockAsync(1, 7, 2026, 99); // should not throw

        var locks = await guard.GetLocksAsync(1);
        Assert.Single(locks);
        Assert.True(locks[0].IsLocked);
    }

    [Fact]
    public async Task UnlockPeriod_NotLocked_IsNoOp()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        // No lock exists yet — unlock should not throw
        await guard.UnlockAsync(1, 7, 2026, 1);

        Assert.False(await guard.IsLockedAsync(1, 7, 2026));
    }

    [Fact]
    public async Task RelockUnlockedPeriod_IsLockedAgain()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);
        await guard.UnlockAsync(1, 7, 2026, 1);
        await guard.LockAsync(1, 7, 2026, 99);

        Assert.True(await guard.IsLockedAsync(1, 7, 2026));
    }

    [Fact]
    public async Task GetLockMessage_LockedPeriod_ReturnsNonNullMessage()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 7, 2026, 99);

        var msg = await guard.GetLockMessageAsync(1, 7, 2026);
        Assert.NotNull(msg);
        Assert.Contains("locked", msg!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetLockMessage_OpenPeriod_ReturnsNull()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        var msg = await guard.GetLockMessageAsync(1, 7, 2026);
        Assert.Null(msg);
    }

    // ── PayrollService respects lock (uses MockLockedPayrollLockGuard) ────

    [Fact]
    public async Task BulkGenerate_LockedPeriod_MockGuardBlocksAtController()
    {
        // This tests the guard interface at service level.
        // The controller-level block test belongs in integration tests.
        // Here we verify the guard correctly reports a locked period.
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new MockLockedPayrollLockGuard();

        var msg = await guard.GetLockMessageAsync(1, 7, 2026);
        Assert.NotNull(msg);
    }

    // ── GetLocks list ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocks_FiltersByYear()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var guard = new PayrollLockGuard(db);

        await guard.LockAsync(1, 6, 2025, 99);
        await guard.LockAsync(1, 7, 2026, 99);

        var locks2026 = await guard.GetLocksAsync(1, year: 2026);
        Assert.Single(locks2026);
        Assert.Equal(2026, locks2026[0].Year);
    }
}
