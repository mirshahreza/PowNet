using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PowNet.Services;
using Xunit;

namespace PowNet.Test.Services
{
    public class ApiCallServiceNegativeTests
    {
        [Fact]
        public void GetApiCallInfo_Should_Throw_When_RouteValues_Missing()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/api/missing";
            Action act = () => ctx.GetApiCallInfo();
            act.Should().Throw<Exception>();
        }
    }
}
