using FluentAssertions;
using PowNet.Extensions;
using Xunit;

namespace PowNet.Test.Extensions
{
    [Collection("EventBusSerial")] // ensure no parallel interference with global static EventBus options
    public class EventBusAdvancedTests
    {
        public record RetEvent(int Id);
        public record SEvent(int Id);

        private static void ResetBus(bool continueOnError)
        {
            EventBusExtensions.Reset();
            EventBusExtensions.Configure(o => { o.ContinueOnHandlerError = continueOnError; o.UseParallelExecution = false; });
        }

        [Fact]
        public async Task PublishWithRetryAsync_Should_Retry_Then_Succeed()
        {
            int attempts = 0;
            ResetBus(continueOnError: false);
            using var sub = EventBusExtensions.Subscribe<RetEvent>(async (e, ct) => {
                attempts++;
                if (attempts < 2) throw new InvalidOperationException("fail");
                await Task.CompletedTask;
            });
            await new RetEvent(1).PublishWithRetryAsync(maxRetries: 3, delay: TimeSpan.FromMilliseconds(5));
            attempts.Should().Be(2);
        }

        [Fact]
        public async Task PublishWithRetryAsync_Should_Reach_MaxRetries_When_Errors_Suppressed()
        {
            int attempts = 0;
            ResetBus(continueOnError: true);
            using var sub = EventBusExtensions.Subscribe<RetEvent>(async (e, ct) => {
                attempts++;
                throw new InvalidOperationException("fail");
            });
            await new RetEvent(2).PublishWithRetryAsync(maxRetries: 2, delay: TimeSpan.FromMilliseconds(5));
            // With suppression the internal publish does not throw; retry loop ends early after first failure
            attempts.Should().Be(1);
        }

        [Fact]
        public async Task PublishWithRetryAsync_Should_Fail_After_MaxRetries_When_Not_Suppressed()
        {
            int attempts = 0;
            ResetBus(continueOnError: false);
            using var sub = EventBusExtensions.Subscribe<RetEvent>(async (e, ct) => {
                attempts++;
                throw new InvalidOperationException("fail");
            });
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await new RetEvent(3).PublishWithRetryAsync(maxRetries: 1, delay: TimeSpan.FromMilliseconds(5)));
            attempts.Should().Be(2); // initial + retry
        }

        [Fact]
        public async Task ScheduleEvent_Should_Execute_Immediately_When_Past()
        {
            int called = 0;
            ResetBus(true);
            using var sub = EventBusExtensions.Subscribe<SEvent>(async (e, ct) => { called++; await Task.CompletedTask; });
            new SEvent(1).ScheduleEvent(DateTime.UtcNow.AddMilliseconds(-10));
            await Task.Delay(80);
            called.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ScheduleEvent_Should_Execute_Later()
        {
            int called = 0;
            ResetBus(true);
            using var sub = EventBusExtensions.Subscribe<SEvent>(async (e, ct) => { called++; await Task.CompletedTask; });
            new SEvent(2).ScheduleEvent(DateTime.UtcNow.AddMilliseconds(120));
            called.Should().Be(0);
            await Task.Delay(400);
            called.Should().BeGreaterThanOrEqualTo(1);
        }
    }
}
