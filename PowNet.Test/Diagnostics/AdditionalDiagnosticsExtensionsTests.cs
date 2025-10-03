using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Diagnostics
{
    public class AdditionalDiagnosticsExtensionsTests
    {
        [Fact]
        public void Measure_Should_Return_Result_And_Elapsed()
        {
            TimeSpan elapsed;
            var res = ((Func<int>)(() => 42)).Measure(out elapsed);
            res.Should().Be(42);
            elapsed.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        }

        [Fact]
        public async Task MeasureAsync_Should_Return_Result_And_Call_Callback()
        {
            TimeSpan seen = TimeSpan.Zero;
            Func<Task<int>> fx = async () => { await Task.Delay(5); return 7; };
            var res = await fx.MeasureAsync(t => seen = t);
            res.Should().Be(7);
            seen.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        }

        [Fact]
        public void GetExceptionChain_Should_Return_Chain()
        {
            var ex = new InvalidOperationException("outer", new ArgumentException("inner"));
            var list = ex.GetExceptionChain().ToList();
            list.Count.Should().Be(2);
            list[0].Message.Should().Contain("outer");
            list[1].Message.Should().Contain("inner");
        }
    }
}
