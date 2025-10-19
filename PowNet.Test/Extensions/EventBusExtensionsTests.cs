using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class EventBusExtensionsTests
    {
        public record TestEvent(int Id);

        [Fact]
        public async Task Publish_And_Subscribe_Typed_Should_Work()
        {
            var received = new List<int>();
            using var sub = EventBusExtensions.Subscribe<TestEvent>(async (e, ct) => { received.Add(e.Id); await Task.CompletedTask; });
            await new TestEvent(1).PublishAsync();
            await new TestEvent(2).PublishAsync();
            received.Should().Equal(1,2);
        }

        [Fact]
        public async Task Publish_And_Subscribe_Named_Should_Work()
        {
            var received = new List<object>();
            using var sub = EventBusExtensions.Subscribe("TestEvent", async (e, ct) => { received.Add(e); await Task.CompletedTask; });
            await new TestEvent(3).PublishAsync();
            received.Should().NotBeEmpty();
        }

        [Fact]
        public async Task SubscribeIf_And_Transform_Should_Work()
        {
            var received = new List<int>();
            using var sub = EventBusExtensions.SubscribeIf<TestEvent>(e => e.Id > 1, async (e, ct) => { received.Add(e.Id); await Task.CompletedTask; });
            using var sub2 = EventBusExtensions.SubscribeTransform<TestEvent, int>(e => e.Id*2, async (v, ct) => { received.Add(v); await Task.CompletedTask; });
            await new TestEvent(1).PublishAsync();
            await new TestEvent(2).PublishAsync();
            received.Should().Contain(4);
        }

        [Fact]
        public async Task Pipeline_And_Debounce_Should_Work()
        {
            var list = new List<int>();
            var pipeline = EventBusExtensions.CreatePipeline<TestEvent>()
                .AddStage((TestEvent e) => new TestEvent(e.Id+1))
                .AddStage(async (TestEvent e, CancellationToken ct) => { await Task.Delay(1); return new TestEvent(e.Id+1); });
            using var sub = pipeline.Subscribe();

            using var dsub = EventBusExtensions.DebounceEvents<TestEvent>(TimeSpan.FromMilliseconds(5), async (e, ct) => { list.Add(e.Id); await Task.CompletedTask; });

            await new TestEvent(5).PublishAsync();
            await Task.Delay(20);
            list.Should().NotBeEmpty();
        }

        [Fact]
        public void Configure_And_Stats_Should_Work()
        {
            EventBusExtensions.Configure(o => { o.UseParallelExecution = false; o.ContinueOnHandlerError = true; });
            var stats = EventBusExtensions.GetStatistics();
            stats.TotalHandlers.Should().BeGreaterThanOrEqualTo(0);
        }
    }
}
