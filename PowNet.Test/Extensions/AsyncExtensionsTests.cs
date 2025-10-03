using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class AsyncExtensionsTests
    {
        [Fact]
        public async Task ExecuteWithConcurrency_Should_Run_All_Factories()
        {
            var list = new List<int> { 1, 2, 3, 4 };
            var factories = list.Select(i => (Func<Task<int>>)(async () => { await Task.Delay(10); return i * 2; }));
            var results = await factories.ExecuteWithConcurrency(maxConcurrency: 2);
            results.Should().BeEquivalentTo(new[] { 2, 4, 6, 8 });
        }

        [Fact]
        public async Task RetryWithBackoff_Should_Retry_On_Exception()
        {
            int attempts = 0;
            Func<Task<int>> f = async () =>
            {
                attempts++;
                if (attempts < 2) throw new InvalidOperationException();
                await Task.Delay(1);
                return 42;
            };
            var res = await f.RetryWithBackoff(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));
            res.Should().Be(42);
        }

        [Fact]
        public async Task WithTimeout_Should_Throw_On_Timeout()
        {
            var task = Task.Delay(50).ContinueWith(_ => 1);
            Func<Task> act = async () => await task.WithTimeout(TimeSpan.FromMilliseconds(1));
            await act.Should().ThrowAsync<TimeoutException>();
        }

        [Fact]
        public async Task WithCircuitBreaker_Should_Open_On_Failures()
        {
            var breakerName = Guid.NewGuid().ToString();
            int attempts = 0;
            Func<Task<int>> failing = async () => { attempts++; throw new Exception("fail"); };
            // first threshold failures cause InvalidOperationException on next call
            await Assert.ThrowsAsync<Exception>(async () => await failing.WithCircuitBreaker(breakerName, failureThreshold: 1));
            await Task.Delay(10);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await failing.WithCircuitBreaker(breakerName, failureThreshold: 1));
        }

        [Fact]
        public async Task ProcessParallelAsync_Should_Process_All()
        {
            var items = Enumerable.Range(1, 10).ToList();
            var results = await items.ProcessParallelAsync(async i => { await Task.Delay(1); return i * i; }, maxConcurrency: 3);
            results.OrderBy(x => x).Should().Equal(items.Select(i => i * i));
        }

        [Fact]
        public async Task ProcessInBatchesAsync_Should_Report_Progress()
        {
            var items = Enumerable.Range(1, 10).ToList();
            var reports = new List<BatchProgress>();
            var progress = new Progress<BatchProgress>(bp => reports.Add(bp));
            await items.ProcessInBatchesAsync(async batch => await Task.Delay(1), batchSize: 3, progress: progress);
            reports.Should().NotBeEmpty();
            reports.Last().ProcessedItems.Should().Be(10);
        }

        [Fact]
        public async Task MeasureAsync_And_TrackMetrics_Should_Record()
        {
            AsyncMetricsCollector.ClearMetrics();
            var t = Task.FromResult(5);
            var pr = await t.MeasureAsync("op");
            pr.IsSuccess.Should().BeTrue();
            var value = await t.TrackMetrics("op");
            value.Should().Be(5);
            var metrics = AsyncMetricsCollector.GetMetrics();
            metrics.Should().ContainKey("op");
        }

        [Fact]
        public async Task CacheAsync_Should_Cache_Results()
        {
            var key = "X::" + Guid.NewGuid();
            int calls = 0;
            Func<Task<int>> f = async () => { calls++; await Task.Delay(1); return 7; };
            var a = await AsyncExtensions.CacheAsync(f, key, TimeSpan.FromMinutes(1));
            var b = await AsyncExtensions.CacheAsync(f, key, TimeSpan.FromMinutes(1));
            a.Should().Be(7);
            b.Should().Be(7);
            calls.Should().Be(1);
        }
    }
}
