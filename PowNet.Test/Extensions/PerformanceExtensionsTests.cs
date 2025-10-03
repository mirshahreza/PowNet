using System.Linq;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class PerformanceExtensionsTests
    {
        [Fact]
        public async Task Memoize_Variants_Should_Cache()
        {
            int c1 = 0;
            Func<int> f1 = () => ++c1;
            var m1 = f1.Memoize();
            m1().Should().Be(1);
            m1().Should().Be(1);

            int c2 = 0;
            Func<int,int> f2 = x => { c2++; return x*x; };
            var m2 = f2.Memoize();
            m2(2).Should().Be(4);
            m2(2).Should().Be(4);
            c2.Should().Be(1);

            int c3 = 0;
            Func<Task<int>> f3 = async () => { c3++; await Task.Delay(1); return 7; };
            var m3 = f3.MemoizeAsync();
            (await m3()).Should().Be(7);
            (await m3()).Should().Be(7);
            c3.Should().Be(1);
        }

        [Fact]
        public async Task MemoizeWithExpiration_Should_Expire()
        {
            int c = 0;
            Func<int> f = () => ++c;
            var m = f.MemoizeWithExpiration(TimeSpan.FromMilliseconds(5));
            m().Should().Be(1);
            await Task.Delay(10);
            m().Should().Be(2);
        }

        [Fact]
        public async Task Batch_Processing_Should_Work()
        {
            var items = Enumerable.Range(1, 20).ToList();
            int sum = 0;
            await items.ProcessInBatchesAsync(async batch => { await Task.Delay(1); Interlocked.Add(ref sum, batch.Sum()); }, 5, 2);
            sum.Should().Be(items.Sum());

            var outItems = await items.TransformInBatchesAsync(async x => { await Task.Delay(1); return x*x; }, 5, 2);
            outItems.OrderBy(x=>x).Should().Equal(items.Select(x=>x*x));
        }

        [Fact]
        public async Task Lazy_And_Pipeline_Should_Work()
        {
            var lazy = PerformanceExtensions.CreateLazy(() => 10);
            lazy.Value.Should().Be(10);

            var alazy = PerformanceExtensions.CreateAsyncLazy(async () => { await Task.Delay(1); return 11; });
            (await alazy.Value).Should().Be(11);

            var pipe = new[]{1,2,3}.AsEnumerable().CreatePipeline()
                .AddStage(x => x+1)
                .AddAsyncStage(async x => { await Task.Delay(1); return x*2; });
            pipe.ToArray().Should().Equal(4,6,8);
        }

        [Fact]
        public void ProcessLarge_And_GC_Should_Work()
        {
            var seq = Enumerable.Range(1, 1000);
            var outSeq = seq.ProcessLarge(x => x+1, 128);
            outSeq.Take(3).Should().Equal(2,3,4);
            PerformanceExtensions.ForceGarbageCollection().Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public async Task Retry_Async_And_Sync_Should_Work()
        {
            int a1 = 0;
            Func<Task<int>> f1 = async () => { a1++; if (a1 < 2) throw new InvalidOperationException(); await Task.Delay(1); return 5; };
            var r1 = await f1.RetryAsync(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));
            r1.Should().Be(5);

            int a2 = 0;
            Func<int> f2 = () => { a2++; if (a2 < 2) throw new InvalidOperationException(); return 6; };
            var r2 = f2.Retry(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(1));
            r2.Should().Be(6);
        }

        [Fact]
        public async Task Monitor_Performance_Should_Log_Slow_And_Fast()
        {
            var fast = new Func<int>(() => 1).MonitorPerformance(slowThreshold: TimeSpan.FromMilliseconds(100));
            fast.Should().Be(1);

            var slow = await new Func<Task<int>>(async () => { await Task.Delay(5); return 2; })
                .MonitorPerformanceAsync(slowThreshold: TimeSpan.FromMilliseconds(1));
            slow.Should().Be(2);
        }
    }
}
