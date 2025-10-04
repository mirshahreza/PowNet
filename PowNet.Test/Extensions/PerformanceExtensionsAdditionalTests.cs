using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class PerformanceExtensionsAdditionalTests
    {
        [Fact]
        public void MemoizeWithExpiration_Should_Recompute_After_Expiry()
        {
            int calls = 0;
            Func<int> f = () => { calls++; return calls; };
            var memo = f.MemoizeWithExpiration(TimeSpan.FromMilliseconds(30));
            var a = memo();
            var b = memo();
            a.Should().Be(b); // cached
            calls.Should().Be(1);
            Thread.Sleep(50);
            var c = memo();
            calls.Should().Be(2);
            c.Should().BeGreaterThan(a);
        }

        [Fact]
        public void ForceGarbageCollection_Should_Return_MemoryDelta()
        {
            // Accept any value; just ensure method executes without exception
            Action act = () => { var _ = PerformanceExtensions.ForceGarbageCollection(); };
            act.Should().NotThrow();
        }
    }
}
