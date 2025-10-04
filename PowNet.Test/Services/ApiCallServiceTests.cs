using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PowNet.Services;
using Xunit;

namespace PowNet.Test.Services
{
    public class ApiCallServiceTests
    {
        [Fact]
        public void GetApiCallInfo_Should_Extract_Route_Data()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/api/v1/users/list";

            var rd = new RouteData();
            rd.Values["controller"] = "Users";
            rd.Values["action"] = "List";
            ctx.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = rd });

            var info = ctx.GetApiCallInfo();
            info.ControllerName.Should().Be("Users");
            info.ApiName.Should().Be("List");
            info.RequestPath.Should().Be("/api/v1/users/list");
            info.NamespaceName.Should().NotBeNull();
        }

        [Fact]
        public void GetCacheKey_Should_Respect_PerUser()
        {
            var apiInfo = new ApiCallInfo("/api/x", "api", "Users", "Get");
            var apiCfg = new PowNet.Configuration.ApiConfiguration { ApiName = "Get", CacheLevel = PowNet.Common.CacheLevel.PerUser, CacheSeconds = 60 };
            var uso = new PowNet.Models.UserServerObject { Id = 1, UserName = "alice" };
            var key = apiInfo.GetCacheKey(apiCfg, uso);
            key.Should().Be("Response::Users_Get_alice");
        }
    }
}
