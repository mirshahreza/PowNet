using System.Threading.Tasks;
using FluentAssertions;
using PowNet.Common;
using PowNet.Configuration;
using Xunit;

namespace PowNet.Test.Configuration
{
    public class ApiConfigurationExtensionsTests
    {
        [Fact]
        public void IsCachingEnabled_Should_Return_True_For_PerUser_With_Positive_Seconds()
        {
            var c = new ApiConfiguration{ ApiName="Get", CacheLevel = CacheLevel.PerUser, CacheSeconds = 10};
            c.IsCachingEnabled().Should().BeTrue();
        }

        [Fact]
        public void IsCachingEnabled_Should_Return_False_When_Seconds_Zero()
        {
            var c = new ApiConfiguration{ ApiName="Get", CacheLevel = CacheLevel.PerUser, CacheSeconds = 0};
            c.IsCachingEnabled().Should().BeFalse();
        }

        [Fact]
        public void GetCacheOptions_Should_Set_AbsoluteExpiration()
        {
            var c = new ApiConfiguration{ ApiName="Get", CacheLevel = CacheLevel.AllUsers, CacheSeconds = 5};
            var opt = c.GetCacheOptions();
            opt.AbsoluteExpirationRelativeToNow.Should().NotBeNull();
        }
    }
}
