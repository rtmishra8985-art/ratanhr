using HRMS.Application.DTOs.Holiday;
using HRMS.Infrastructure.Services;
using HRMS.Tests.Mocks;
using Xunit;

namespace HRMS.Tests;

public class HolidayServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidDate_CreatesHoliday()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new HolidayService(db, new MockCacheService());

        var result = await svc.CreateAsync(companyId: 1, new CreateHolidayDto {
            Name = "Diwali", Date = "2026-10-20", Description = "Festival of Lights"
        });

        Assert.Equal("Diwali", result.Name);
        Assert.Equal("2026-10-20", result.Date);
        Assert.Equal(1, result.CompanyId);
    }

    [Fact]
    public async Task CreateAsync_InvalidDate_Throws()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new HolidayService(db, new MockCacheService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(1, new CreateHolidayDto { Name = "Bad", Date = "not-a-date" }));
    }

    [Fact]
    public async Task GetAllAsync_FiltersGlobalAndCompanyHolidays()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new HolidayService(db, new MockCacheService());

        await svc.CreateAsync(null, new CreateHolidayDto { Name = "Global Republic Day", Date = "2026-01-26" });
        await svc.CreateAsync(1,    new CreateHolidayDto { Name = "Company Founders Day",  Date = "2026-03-15" });
        await svc.CreateAsync(2,    new CreateHolidayDto { Name = "Other Company Holiday",  Date = "2026-04-01" });

        var list = await svc.GetAllAsync(companyId: 1, year: 2026);

        // Should include global (companyId null) and company 1 but NOT company 2
        Assert.Equal(2, list.Count);
        Assert.Contains(list, h => h.Name == "Global Republic Day");
        Assert.Contains(list, h => h.Name == "Company Founders Day");
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var svc = new HolidayService(db, new MockCacheService());

        var h = await svc.CreateAsync(1, new CreateHolidayDto { Name = "Test Holiday", Date = "2026-06-01" });
        var ok = await svc.DeleteAsync(h.Id, null, true);
        Assert.True(ok);

        var result = await svc.GetByIdAsync(h.Id, null);
        Assert.NotNull(result);          // Still retrievable by ID
        Assert.False(result!.IsActive);  // But marked inactive

        var list = await svc.GetAllAsync(companyId: 1, year: 2026);
        Assert.DoesNotContain(list, x => x.Id == h.Id);  // Filtered from list
    }
}
