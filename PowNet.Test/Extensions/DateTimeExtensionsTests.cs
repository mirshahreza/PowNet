using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void ToAppEndStandard_Should_Format_Correctly()
        {
            var dt = new DateTime(2024, 7, 15, 13, 5, 9);
            var s = dt.ToAppEndStandard();
            s.Should().StartWith("2024-07-15 13:05:09 ");
            // suffix is AM/PM, culture-invariant pattern's tt placeholder
            (s.EndsWith("AM") || s.EndsWith("PM")).Should().BeTrue();
        }

        [Fact]
        public void AdditionalDateHelpers_Should_Work()
        {
            var dt = new DateTime(2024, 7, 15, 13, 5, 23, DateTimeKind.Utc);
            dt.RoundTo(TimeSpan.FromMinutes(1)).Should().Be(new DateTime(2024, 7, 15, 13, 5, 0, DateTimeKind.Utc));
            dt.FloorTo(TimeSpan.FromMinutes(5)).Should().Be(new DateTime(2024, 7, 15, 13, 5, 0, DateTimeKind.Utc));
            dt.CeilingTo(TimeSpan.FromMinutes(5)).Should().Be(new DateTime(2024, 7, 15, 13, 10, 0, DateTimeKind.Utc));
            dt.IsBetween(dt.AddMinutes(-1), dt.AddMinutes(1)).Should().BeTrue();
            dt.Next(DayOfWeek.Monday).DayOfWeek.Should().Be(DayOfWeek.Monday);
        }
    }
}
