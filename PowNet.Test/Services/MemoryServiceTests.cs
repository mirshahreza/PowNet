using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using PowNet.Services;

namespace PowNet.Test.Services
{
    public class MemoryServiceTests
    {
        [Fact]
        public void SharedMemoryCache_Basic_Set_Get_Remove_Should_Track_Stats()
        {
            var cache = MemoryService.SharedMemoryCache;
            MemoryService.ClearStats();

            cache.TryAdd("cat::k1", 123, TimeSpan.FromMinutes(5));
            cache.TryAdd("cat::k2", "value", TimeSpan.FromMinutes(5));

            cache.GetWithStats<int>("cat::k1").Should().Be(123);
            cache.GetWithStats<string>("cat::k2").Should().Be("value");
            cache.GetWithStats<string>("cat::missing").Should().BeNull();

            cache.TryRemove("cat::k1");

            var metrics = MemoryService.GetCacheMetrics();
            metrics.TotalHits.Should().Be(2);
            metrics.TotalMisses.Should().Be(1);
            metrics.TotalSets.Should().BeGreaterOrEqualTo(2);
            metrics.TotalRemovals.Should().BeGreaterOrEqualTo(1);
            metrics.HitRatio.Should().BeApproximately(2.0/3.0, 0.5);

            metrics.CategoryStats.Should().ContainKey("cat");
            metrics.ActiveKeysCount.Should().BeGreaterOrEqualTo(1);
        }

        [Fact]
        public void GetKeys_Helper_Methods_Should_Work()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            cache.TryAdd("A::1", 1);
            cache.TryAdd("A::2", 2);
            cache.TryAdd("B::3", 3);

            var keys = cache.GetKeys<string>().ToList();
            keys.Should().Contain(new[] { "A::1", "A::2", "B::3" });

            var startsWithA = cache.GetKeysStartsWith("A::");
            startsWithA.Should().BeEquivalentTo(new[] { "A::1", "A::2" });
        }

        [Fact]
        public void CategoryStats_Should_Increment_On_Access()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            MemoryService.ClearStats();

            cache.TryAdd("C::1", 1);
            cache.GetWithStats<int>("C::1");
            cache.GetWithStats<int>("C::missing");

            var cat = MemoryService.GetCategoryStats("C");
            cat.Should().NotBeNull();
            var snap = cat!.GetSnapshot();
            (snap.Hits + snap.Misses).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task WarmUpCacheAsync_Should_Add_Keys_And_Not_Throw_On_Failure()
        {
            var data = new List<KeyValuePair<string, Func<Task<object>>>>
            {
                new("W::ok", async () => { await Task.Delay(5); return 123; }),
                new("W::fail", async () => { await Task.Delay(5); throw new Exception("x"); })
            };

            await MemoryService.WarmUpCacheAsync(data);
            MemoryService.SharedMemoryCache.Get<int>("W::ok").Should().Be(123);
        }
    }
}
