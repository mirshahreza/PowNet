using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class StringArrayExtensionsTests
    {
        [Fact]
        public void ContainsIgnoreCase_Should_Work()
        {
            string[] a = new[] { "Alpha", "Beta" };
            a.ContainsIgnoreCase("alpha").Should().BeTrue();
            a.ContainsIgnoreCase("gamma").Should().BeFalse();
            ((string[]?)null).ContainsIgnoreCase("alpha").Should().BeFalse();
        }

        [Fact]
        public void HasIntersect_Should_Work()
        {
            string[] a = new[] { "a", "b" };
            string[] b = new[] { "B", "c" };
            a.HasIntersect(b).Should().BeTrue();
            a.HasIntersect(Array.Empty<string>()).Should().BeFalse();
            a.HasIntersect(null).Should().BeFalse();
        }
    }
}
