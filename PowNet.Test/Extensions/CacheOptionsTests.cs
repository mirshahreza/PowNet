using System.Threading.Tasks;
using FluentAssertions;
using PowNet.Extensions;
using Xunit;

namespace PowNet.Test.Extensions
{
    public class CacheOptionsTests
    {
        [Fact]
        public async Task CacheAsync_Should_Not_Cache_Null_When_Disabled()
        {
            var key = "CNULL::" + Guid.NewGuid();
            Func<Task<string?>> factory = async () => { await Task.Delay(1); return null; };
            var options = new CacheOptions{ CacheNullValues = false };
            var val1 = await factory.CacheAsync(key, TimeSpan.FromMilliseconds(50), options: options);
            var val2 = await factory.CacheAsync(key, TimeSpan.FromMilliseconds(50), options: options);
            val1.Should().BeNull();
            val2.Should().BeNull(); // both executions (not cached)
        }

        [Fact]
        public async Task CacheAsync_Should_Cache_Empty_String_When_Enabled()
        {
            var key = "CEMPTY::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<string>> factory = async () => { calls++; await Task.Delay(1); return string.Empty; };
            var options = new CacheOptions{ CacheEmptyStrings = true };
            var v1 = await factory.CacheAsync(key, TimeSpan.FromMilliseconds(50), options: options);
            var v2 = await factory.CacheAsync(key, TimeSpan.FromMilliseconds(50), options: options);
            calls.Should().Be(1);
            v1.Should().BeEmpty();
            v2.Should().BeEmpty();
        }

        [Fact]
        public async Task CacheAsync_Should_Not_Cache_Empty_Collection_When_Disabled()
        {
            var key = "CEMPTYLIST::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<List<int>>> factory = async () => { calls++; await Task.Delay(1); return new List<int>(); };
            var options = new CacheOptions{ CacheEmptyCollections = false };
            var v1 = await factory.CacheAsync(key, TimeSpan.FromMilliseconds(50), options: options);
            var v2 = await factory.CacheAsync(key, TimeSpan.FromMilliseconds(50), options: options);
            calls.Should().Be(2);
            v1.Should().BeEmpty();
            v2.Should().BeEmpty();
        }
    }
}
