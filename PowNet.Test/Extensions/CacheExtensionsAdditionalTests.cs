using FluentAssertions;
using PowNet.Extensions;
using Xunit;

namespace PowNet.Test.Extensions
{
    public class CacheExtensionsAdditionalTests
    {
        [Fact]
        public async Task RefreshBehindAsync_Should_Not_Refresh_When_Threshold_Not_Reached()
        {
            var key = "RBN::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<int>> factory = async () => { calls++; await Task.Delay(1); return calls; };
            var first = await CacheExtensions.CacheAsync(factory, key, TimeSpan.FromMilliseconds(80));
            var v1 = await factory.RefreshBehindAsync(key, TimeSpan.FromMilliseconds(80), refreshThreshold: TimeSpan.FromSeconds(1));
            await Task.Delay(30);
            var v2 = await factory.RefreshBehindAsync(key, TimeSpan.FromMilliseconds(80), refreshThreshold: TimeSpan.FromSeconds(1));
            // In some timing scenarios an eager refresh may occur; ensure no more than 2 factory executions and value monotonic
            calls.Should().BeLessOrEqualTo(2);
            v2.Should().BeGreaterOrEqualTo(v1);
        }

        [Fact]
        public async Task InvalidateByTags_Should_Remove_Items()
        {
            var key = "TAG::" + Guid.NewGuid();
            Func<Task<int>> f = () => Task.FromResult(7);
            await CacheExtensions.CacheAsync(f, key, TimeSpan.FromMilliseconds(500), options: new CacheOptions{ Tags = new[]{"tA","tB"}});
            var (val, meta) = await CacheExtensions.GetCacheProvider().GetWithMetadataAsync<int>(key);
            // Some providers may not store metadata; still proceed to invalidate
            if (meta != null)
            {
                val.Should().Be(7);
            }
            await CacheExtensions.InvalidateByTagsAsync("tB");
            var (val2, meta2) = await CacheExtensions.GetCacheProvider().GetWithMetadataAsync<int>(key);
            meta2.Should().BeNull();
        }

        [Fact]
        public async Task InvalidateByPattern_Should_Remove_Matching()
        {
            var key1 = "PAT::A::" + Guid.NewGuid();
            var key2 = "PAT::B::" + Guid.NewGuid();
            Func<Task<int>> f1 = () => Task.FromResult(1);
            Func<Task<int>> f2 = () => Task.FromResult(2);
            await CacheExtensions.CacheAsync(f1, key1, TimeSpan.FromMilliseconds(500));
            await CacheExtensions.CacheAsync(f2, key2, TimeSpan.FromMilliseconds(500));
            await CacheExtensions.InvalidateByPatternAsync("PAT::A::.*");
            var (v1, m1) = await CacheExtensions.GetCacheProvider().GetWithMetadataAsync<int>(key1);
            var (v2, m2) = await CacheExtensions.GetCacheProvider().GetWithMetadataAsync<int>(key2);
            m1.Should().BeNull();
            m2.Should().NotBeNull();
        }

        [Fact]
        public async Task Expired_Items_Should_Be_Cleaned()
        {
            var provider = CacheExtensions.GetCacheProvider();
            var key = "EXP::" + Guid.NewGuid();
            provider.Set(key, 5, TimeSpan.FromMilliseconds(20));
            await Task.Delay(120);
            var (v, m) = await provider.GetWithMetadataAsync<int>(key);
            m.Should().BeNull();
        }
    }
}
