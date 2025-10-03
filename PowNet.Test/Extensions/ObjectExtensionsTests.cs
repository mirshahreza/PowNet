using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using PowNet.Extensions;
using PowNet.Services;

namespace PowNet.Test.Extensions
{
    public class ObjectExtensionsTests
    {
        [Fact]
        public void ToStringEmpty_Should_Return_Empty_On_Null()
        {
            ((object?)null).ToStringEmpty().Should().Be("");
            123.ToStringEmpty().Should().Be("123");
        }

        [Fact]
        public void ToIntSafe_Should_Parse_Or_Return_Default()
        {
            ((object?)null).ToIntSafe(7).Should().Be(7);
            ((object)"42").ToIntSafe().Should().Be(42);
            ((object)"x").ToIntSafe(5).Should().Be(5);
        }

        [Fact]
        public void ToIntSafeNull_Should_Parse_Or_Return_Null()
        {
            ((object?)null).ToIntSafeNull().Should().BeNull();
            ((object)"42").ToIntSafeNull().Should().Be(42);
            ((object)"x").ToIntSafeNull().Should().BeNull();
        }

        [Fact]
        public void ToDateTimeSafe_Should_Parse_Or_Return_Fallback()
        {
            var dt = new DateTime(2023,1,2,3,4,5);
            ((object)"2023-01-02 03:04:05").ToDateTimeSafe(null).Should().Be(dt);
            var fallback = new DateTime(2000,1,1);
            ((object)"notdt").ToDateTimeSafe(fallback).Should().Be(fallback);
        }

        [Fact]
        public void ToBooleanSafe_Should_Parse_Or_Return_Default()
        {
            ((object?)null).ToBooleanSafe(true).Should().BeTrue();
            ((object)"true").ToBooleanSafe().Should().BeTrue();
            ((object)"x").ToBooleanSafe(true).Should().BeTrue();
        }

        [Fact]
        public void To01Safe_Should_Map_Bool_To_Int()
        {
            true.To01Safe().Should().Be(1);
            false.To01Safe().Should().Be(0);
        }

        [Fact]
        public void FixNull_Should_Return_IfNull_When_Object_Is_Null()
        {
            ((object?)null).FixNull("alt").Should().Be("alt");
            ("x" as object).FixNull("alt").Should().Be("x");
            Action act = () => ((object?)null).FixNull(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Cache_Extensions_Should_Add_And_Remove_From_Shared_Cache()
        {
            MemoryService.ClearStats();
            var key = "T::k" + Guid.NewGuid();
            ((object?)123).AddCache(key);
            MemoryService.SharedMemoryCache.Get<int>(key).Should().Be(123);
            ((object?)null).RemoveCache(key);
            MemoryService.SharedMemoryCache.Get<int>(key).Should().Be(0);
        }
    }
}
