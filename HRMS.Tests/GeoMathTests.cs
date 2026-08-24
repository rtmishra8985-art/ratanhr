using HRMS.Infrastructure.Services;
using Xunit;

namespace HRMS.Tests;

public class GeoMathTests
{
    [Fact]
    public void SamePoint_ReturnsZero() =>
        Assert.Equal(0, GeoMath.HaversineMetres(12.9716, 77.5946, 12.9716, 77.5946), precision: 3);

    [Fact]
    public void BengaluruToMumbai_IsAccurate() =>
        Assert.InRange(GeoMath.HaversineMetres(12.9716, 77.5946, 19.0760, 72.8777), 840_000, 850_000);
}
