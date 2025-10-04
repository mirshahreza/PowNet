using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PowNet.Services;
using Xunit;

#pragma warning disable CS0618 // Obsolete member usage
namespace PowNet.Test.Services
{
    public class ApiCallServiceObsoleteTests
    {
        [Fact]
        public void GetAppEndWebApiInfo_Obsolete_Alias_Should_Work()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/api/v2/orders/get";
            var rd = new RouteData();
            rd.Values["controller"] = "Orders";
            rd.Values["action"] = "Get";
            ctx.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = rd });

            var infoObsolete = ctx.GetAppEndWebApiInfo();
            var infoNew = ctx.GetApiCallInfo();

            infoObsolete.ControllerName.Should().Be(infoNew.ControllerName);
            infoObsolete.ApiName.Should().Be(infoNew.ApiName);
            infoObsolete.RequestPath.Should().Be(infoNew.RequestPath);
        }
    }
}
#pragma warning restore CS0618
