using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;
using PowNet.Security;

namespace PowNet.Test.Security
{
    public class SecurityMiddlewareTests
    {
        [Fact]
        public async Task AddSecurityHeaders_Should_Add_Headers()
        {
            var ctx = new DefaultHttpContext();
            var called = false;
            await SecurityMiddleware.AddSecurityHeaders(ctx, () => { called = true; return Task.CompletedTask; });
            called.Should().BeTrue();
            ctx.Response.Headers.ContainsKey("X-Content-Type-Options").Should().BeTrue();
            ctx.Response.Headers.ContainsKey("X-Frame-Options").Should().BeTrue();
        }

        [Fact]
        public async Task RateLimitingMiddleware_Should_Block_After_Limit()
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            var options = new RateLimitOptions { MaxRequests = 1, TimeWindow = TimeSpan.FromMilliseconds(200) };
            await SecurityMiddleware.RateLimitingMiddleware(ctx, () => Task.CompletedTask, options);
            var ctx2 = new DefaultHttpContext();
            ctx2.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            await SecurityMiddleware.RateLimitingMiddleware(ctx2, () => Task.CompletedTask, options);
            ctx2.Response.StatusCode.Should().Be(429);
        }

        [Fact]
        public async Task InputValidationMiddleware_Should_Return_BadRequest_On_Injection()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "POST";
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");
            ctx.Request.Body = new MemoryStream(bodyBytes);
            await SecurityMiddleware.InputValidationMiddleware(ctx, () => Task.CompletedTask);
            ctx.Response.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task IPFilteringMiddleware_Should_Block_Blocked_IP()
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            var opts = new IPFilterOptions
            {
                BlockedIPRanges = new(){ new IPRange("127.0.0.1","127.0.0.1") }
            };
            await SecurityMiddleware.IPFilteringMiddleware(ctx, () => Task.CompletedTask, opts);
            ctx.Response.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task RequestSizeLimitMiddleware_Should_Block_Large_Content()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.ContentLength = 1024 * 1024 + 1;
            await SecurityMiddleware.RequestSizeLimitMiddleware(ctx, () => Task.CompletedTask, new RequestSizeOptions{ MaxRequestSize = 1024 * 1024 });
            ctx.Response.StatusCode.Should().Be(413);
        }
    }
}
