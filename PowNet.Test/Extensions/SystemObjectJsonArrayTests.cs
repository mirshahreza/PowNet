using System.Text.Json;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class SystemObjectJsonArrayTests
    {
        private void IntParamHelper(int x) { }

        [Fact]
        public void SystemExtensions_Should_Work()
        {
            var mi = typeof(SystemObjectJsonArrayTests).GetMethod(nameof(SystemExtensions_Should_Work))!;
            mi.GetFullName().Should().Contain("SystemObjectJsonArrayTests");
            typeof(SystemObjectJsonArrayTests).GetMethodsReal().Should().NotBeEmpty();
            SystemExtensions.IsRealType("X").Should().BeTrue();
            typeof(SystemObjectJsonArrayTests).Assembly.GetTypesReal().Should().NotBeEmpty();
            SystemExtensions.GetPlaceInfo(mi).Should().Contain("SystemObjectJsonArrayTests");
        }

        [Fact]
        public void ObjectExtensions_Should_Work()
        {
            ((object?)null).ToStringEmpty().Should().BeEmpty();
            ((object?)null).ToIntSafe(5).Should().Be(5);
            ((object?)"x").ToIntSafe(-1).Should().Be(-1);
            ((object?)"1").ToIntSafe(-1).Should().Be(1);
            ((object?)null).ToIntSafeNull().Should().BeNull();
            ((object?)"2").ToIntSafeNull().Should().Be(2);
            ((object?)"true").ToBooleanSafe().Should().BeTrue();
            true.To01Safe().Should().Be(1);
            ((object?)null).FixNull("x").Should().Be("x");
        }

        [Fact]
        public void JsonExtensions_Should_Work()
        {
            var obj = new { a = 1 };
            var s = obj.ToJsonStringByBuiltIn();
            s.Length.Should().BeGreaterThan(0);
            var je = obj.ToJsonElementByBuiltIn();
            je.ValueKind.ToString().Should().NotBeNull();

            // Use string overload to ensure property presence
            var jo2 = "{\"a\":1}".ToJsonObjectByBuiltIn();
            jo2["a"].ToString().Should().Contain("1");

            var arr = "[\"a\",\"b\"]".DeserializeAsStringArray();
            arr.Should().Contain(new[]{"a","b"});

            var val = JsonDocument.Parse("123").RootElement;
            var pinfo = typeof(SystemObjectJsonArrayTests).GetMethod(nameof(IntParamHelper), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetParameters()[0];
            JsonExtensions.ToOrigType(val, pinfo).Should().NotBeNull();

            JsonExtensions.TryDeserializeTo<Dictionary<string,int>>("{\"a\":1}").Should().ContainKey("a");
            JsonExtensions.ToJObjectByNewtonsoft("{\"a\":1}").Should().NotBeNull();
        }
    }
}
