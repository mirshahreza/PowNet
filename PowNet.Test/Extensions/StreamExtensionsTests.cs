using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class StreamExtensionsTests
    {
        [Fact]
        public void ToText_Should_Read_Entire_Stream()
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
            var s = ms.ToText();
            s.Should().Be("hello");
        }

        [Fact]
        public async Task ToTextAsync_Should_Read_Entire_Stream()
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes("world"));
            var s = await ms.ToTextAsync();
            s.Should().Be("world");
        }

        [Fact]
        public void ToJson_Should_Parse_Json()
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes("{\"a\":1}"));
            var el = ms.ToJson();
            el.ValueKind.Should().Be(JsonValueKind.Object);
            el.GetProperty("a").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task ToJsonAsync_Should_Parse_Request_Body()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"b\":2}"));
            var el = await StreamExtensions.ToJsonAsync(ctx.Request);
            el.ValueKind.Should().Be(JsonValueKind.Object);
            el.GetProperty("b").GetInt32().Should().Be(2);
        }
    }
}
