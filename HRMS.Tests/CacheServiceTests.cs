// Regression tests for CacheService (Fix 5)
using HRMS.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HRMS.Tests;

public class CacheServiceTests
{
    private static CacheService Build()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        return new CacheService(memory, NullLogger<CacheService>.Instance);
    }

    [Fact]
    public async Task GetOrSetAsync_CallsFactory_OnFirstCall()
    {
        var svc = Build();
        int factoryCalls = 0;

        var result = await svc.GetOrSetAsync("key1", async () =>
        {
            factoryCalls++;
            await Task.Delay(1);
            return 42;
        });

        Assert.Equal(42, result);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrSetAsync_ReturnsCachedValue_OnSecondCall()
    {
        var svc = Build();
        int factoryCalls = 0;

        await svc.GetOrSetAsync("key2", () => { factoryCalls++; return Task.FromResult("hello"); });
        var second = await svc.GetOrSetAsync("key2", () => { factoryCalls++; return Task.FromResult("world"); });

        Assert.Equal("hello", second);
        Assert.Equal(1, factoryCalls);  // factory should not be called again
    }

    [Fact]
    public async Task RemoveByPrefixAsync_Invalidates_Keys()
    {
        var svc = Build();
        await svc.GetOrSetAsync("prefix:a", () => Task.FromResult(1));
        await svc.GetOrSetAsync("prefix:b", () => Task.FromResult(2));
        await svc.GetOrSetAsync("other:c", () => Task.FromResult(3));

        await svc.RemoveByPrefixAsync("prefix:");

        int calls = 0;
        await svc.GetOrSetAsync("prefix:a", () => { calls++; return Task.FromResult(99); });
        Assert.Equal(1, calls);  // prefix:a should have been evicted, factory called again

        calls = 0;
        await svc.GetOrSetAsync("other:c", () => { calls++; return Task.FromResult(99); });
        Assert.Equal(0, calls);  // other:c should still be cached
    }

    [Fact]
    public async Task RemoveAsync_Invalidates_Single_Key()
    {
        var svc = Build();
        await svc.GetOrSetAsync("rem:key", () => Task.FromResult("original"));

        await svc.RemoveAsync("rem:key");

        int calls = 0;
        await svc.GetOrSetAsync("rem:key", () => { calls++; return Task.FromResult("new"); });
        Assert.Equal(1, calls);
    }
}
