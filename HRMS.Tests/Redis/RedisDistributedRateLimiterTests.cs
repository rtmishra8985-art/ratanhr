// Unit tests for RedisDistributedRateLimiter fail-closed / fail-open behaviour
// when a non-Redis generic Exception is thrown by the pipeline.
using HRMS.Infrastructure.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace HRMS.Tests.Redis;

public class RedisDistributedRateLimiterTests
{
    /// <summary>
    /// Builds a limiter whose IDatabase.CreateBatch() throws <paramref name="ex"/>
    /// so that AcquireAsyncCore lands in the generic catch block.
    /// </summary>
    private static RedisDistributedRateLimiter BuildWithThrow(string policyName, Exception ex)
    {
        var mockDb = new Mock<IDatabase>();
        mockDb
            .Setup(d => d.CreateBatch(It.IsAny<object>()))
            .Throws(ex);

        var mockMux = new Mock<IConnectionMultiplexer>();
        mockMux
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);

        return new RedisDistributedRateLimiter(
            mockMux.Object,
            key:          "ratelimit:test:127.0.0.1",
            policyName:   policyName,
            permitLimit:  5,
            windowSeconds: 60,
            logger:       NullLogger.Instance);
    }

    // ------------------------------------------------------------------ //
    // Fail-closed: sensitive policies must REJECT on generic Exception     //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData("login")]
    [InlineData("sensitive")]
    public async Task GenericException_FailsClosed_ForSensitivePolicy(string policyName)
    {
        using var limiter = BuildWithThrow(policyName, new InvalidOperationException("unexpected boom"));

        using var lease = await limiter.AcquireAsync(permitCount: 1);

        Assert.False(lease.IsAcquired,
            $"Policy '{policyName}' must reject (fail-closed) when a generic Exception is thrown.");
    }

    // ------------------------------------------------------------------ //
    // Fail-open: non-sensitive policies must ALLOW on generic Exception    //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GenericException_FailsOpen_ForApiPolicy()
    {
        using var limiter = BuildWithThrow("api", new InvalidOperationException("unexpected boom"));

        using var lease = await limiter.AcquireAsync(permitCount: 1);

        Assert.True(lease.IsAcquired,
            "Policy 'api' must allow (fail-open) when a generic Exception is thrown.");
    }
}
