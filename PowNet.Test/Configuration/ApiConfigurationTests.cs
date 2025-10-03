using FluentAssertions;
using Xunit;
using PowNet.Configuration;
using PowNet.Common;

namespace PowNet.Test.Configuration
{
    public class ApiConfigurationTests
    {
        [Fact]
        public void ApiConfiguration_Extensions_Should_Work()
        {
            var c = new ApiConfiguration{ CacheLevel = CacheLevel.AllUsers, CacheSeconds = 10, LogEnabled = true };
            c.IsCachingEnabled().Should().BeTrue();
            c.IsLoggingEnabled().Should().BeTrue();
            var opt = c.GetCacheOptions();
            opt.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromSeconds(10));
        }
    }
}
