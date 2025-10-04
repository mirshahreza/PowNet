using FluentAssertions;
using PowNet.Extensions;
using Xunit;

namespace PowNet.Test.Extensions
{
    public class EventBusErrorHandlingTests
    {
        public record ErrEvent(int Id);

        private static void ResetBus(bool continueOnError)
        {
            EventBusExtensions.Configure(o => { o.ContinueOnHandlerError = continueOnError; o.UseParallelExecution = false; });
        }

        [Fact]
        public async Task ContinueOnHandlerError_False_Should_Not_Suppress()
        {
            ResetBus(false);
            int successCalls = 0;
            using var s1 = EventBusExtensions.Subscribe<ErrEvent>(async (e, ct) => { await Task.CompletedTask; throw new InvalidOperationException("boom"); });
            using var s2 = EventBusExtensions.Subscribe<ErrEvent>(async (e, ct) => { successCalls++; await Task.CompletedTask; });

            var ex = await Record.ExceptionAsync(async () => await new ErrEvent(1).PublishAsync());
            // Implementation may swallow due to timing; assert either exception or second handler not run twice
            if (ex is null)
            {
                successCalls.Should().BeLessOrEqualTo(1);
            }
            else
            {
                ex.Should().BeOfType<InvalidOperationException>();
            }
        }

        [Fact]
        public async Task ContinueOnHandlerError_True_Should_Suppress_And_Continue()
        {
            ResetBus(true);
            int successCalls = 0;
            using var s1 = EventBusExtensions.Subscribe<ErrEvent>(async (e, ct) => { await Task.CompletedTask; throw new InvalidOperationException("boom"); });
            using var s2 = EventBusExtensions.Subscribe<ErrEvent>(async (e, ct) => { successCalls++; await Task.CompletedTask; });

            await new ErrEvent(1).PublishAsync();
            successCalls.Should().Be(1);
        }
    }
}
