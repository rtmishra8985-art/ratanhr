using FluentAssertions;
using HRMS.Domain.Entities.Attendance;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Tests;

public class GeoFenceSoftDeleteTests
{
    [Fact]
    public async Task GeoFencesQuery_ExcludesSoftDeletedRows()
    {
        await using var db = TestHelpers.CreateInMemoryDb();

        db.GeoFences.AddRange(
            new GeoFence { Name = "Visible fence", CompanyId = 1 },
            new GeoFence { Name = "Deleted fence", CompanyId = 1, IsDeleted = true });
        await db.SaveChangesAsync();

        var visible = await db.GeoFences.ToListAsync();
        var includingDeleted = await db.GeoFences.IgnoreQueryFilters().ToListAsync();

        visible.Should().ContainSingle();
        visible[0].Name.Should().Be("Visible fence");
        includingDeleted.Should().HaveCount(2);
    }
}