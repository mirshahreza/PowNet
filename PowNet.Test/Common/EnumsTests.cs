using FluentAssertions;
using Xunit;
using PowNet.Common;

namespace PowNet.Test.Common
{
    public class EnumsTests
    {
        [Fact]
        public void Enums_Should_Be_Serializable_As_String()
        {
            var v = System.Text.Json.JsonSerializer.Serialize(ServerType.MsSql);
            v.Should().Contain("\"MsSql\"");
        }
    }
}
