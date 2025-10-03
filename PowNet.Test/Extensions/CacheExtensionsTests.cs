using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class CacheExtensionsTests
    {
        [Fact]
        public async Task CacheAsync_Should_Cache_Result_And_Update_Stats()
        {
            CacheExtensions.ResetStatistics();
            var key = "K::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<int>> f = async () => { calls++; await Task.Delay(1); return 11; };
            var a = await CacheExtensions.CacheAsync(f, key, TimeSpan.FromMilliseconds(100));
            var b = await CacheExtensions.CacheAsync(f, key, TimeSpan.FromMilliseconds(100));
            a.Should().Be(11);
            b.Should().Be(11);
            calls.Should().Be(1);

            var stats = CacheExtensions.GetStatistics();
            (stats.TotalHits + stats.TotalMisses).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task MultiLevelCacheAsync_Should_Populate_Layers()
        {
            var key = "ML::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<int>> f = async () => { calls++; await Task.Delay(1); return 5; };
            var a = await f.MultiLevelCacheAsync(key, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
            var b = await f.MultiLevelCacheAsync(key, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
            a.Should().Be(5);
            b.Should().Be(5);
            calls.Should().Be(1);
        }

        [Fact]
        public async Task CacheIfAsync_Should_Respect_Condition()
        {
            var key = "CI::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<int>> f = async () => { calls++; await Task.Delay(1); return 0; };
            var a = await f.CacheIfAsync(v => v > 0, key, TimeSpan.FromMilliseconds(50));
            var b = await f.CacheIfAsync(v => v > 0, key, TimeSpan.FromMilliseconds(50));
            calls.Should().Be(2);
        }

        [Fact]
        public async Task RefreshBehindAsync_Should_Refresh_In_Background()
        {
            var key = "RB::" + Guid.NewGuid();
            var value = 1;
            Func<Task<int>> f = async () => { await Task.Delay(1); return Interlocked.Increment(ref value); };
            // seed cache with initial value
            var a = await CacheExtensions.CacheAsync(f, key, TimeSpan.FromMilliseconds(100));
            var v1 = await f.RefreshBehindAsync(key, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(0));
            // wait background refresh to happen
            await Task.Delay(20);
            var v2 = await f.RefreshBehindAsync(key, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(0));
            v2.Should().BeGreaterOrEqualTo(v1);
        }

        [Fact]
        public async Task InvalidateByTags_And_Pattern_Should_Execute_Without_Exception()
        {
            await CacheExtensions.InvalidateByTagsAsync("t1","t2");
            await CacheExtensions.InvalidateByPatternAsync(".*");
        }

        [Fact]
        public async Task WarmCache_Should_Populate()
        {
            var key = "W::" + Guid.NewGuid();
            await CacheExtensions.WarmCacheAsync(key, async () => { await Task.Delay(1); return 42; }, TimeSpan.FromMilliseconds(100));
            var fromCache = await CacheExtensions.GetCacheProvider().GetAsync<int>(key);
            fromCache.Should().Be(42);
        }

        [Fact]
        public async Task WarmCacheBatch_Should_Populate_Many()
        {
            var dict = new Dictionary<string, Func<Task<int>>>
            {
                ["B1"] = async () => { await Task.Delay(1); return 1; },
                ["B2"] = async () => { await Task.Delay(1); return 2; }
            };
            await CacheExtensions.WarmCacheBatchAsync(dict, TimeSpan.FromMilliseconds(100));
            var p = await CacheExtensions.GetCacheProvider().GetAsync<int>("B1");
            p.Should().Be(1);
        }
    }
}
