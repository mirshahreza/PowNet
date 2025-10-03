using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class CollectionExtensionsTests
    {
        #region Array Extensions
        [Fact]
        public void FastCopy_PrimitiveStruct_Should_Copy_By_Value()
        {
            var src = new[] { 1, 2, 3, 4 };
            var copy = src.FastCopy();

            copy.Should().NotBeSameAs(src);
            copy.Should().Equal(src);
            src[0] = 99;
            copy[0].Should().Be(1);
        }

        [Fact]
        public void FastCopy_NonPrimitiveStruct_Should_Copy_By_Value()
        {
            var src = new[] { decimal.One, decimal.Zero, 12.34m };
            var copy = src.FastCopy();

            copy.Should().NotBeSameAs(src);
            copy.Should().Equal(src);
        }

        [Fact]
        public void FastIndexOf_Unsorted_Should_Return_Index_Or_Neg1()
        {
            var arr = new[] { 5, 1, 7, 3 };
            arr.FastIndexOf(7, isSorted: false).Should().Be(2);
            arr.FastIndexOf(9, isSorted: false).Should().Be(-1);
        }

        [Fact]
        public void FastIndexOf_Sorted_Should_Use_BinarySearch_Semantics()
        {
            var arr = new[] { 1, 3, 5, 7 };
            arr.FastIndexOf(5, isSorted: true).Should().Be(2);
            var notFound = arr.FastIndexOf(6, isSorted: true);
            notFound.Should().BeLessThan(0); // insertion point encoded as negative-1
        }

        [Fact]
        public void ProcessInBatches_Should_Process_All_Items_In_Batches()
        {
            var arr = Enumerable.Range(1, 10).ToArray();
            var batches = new List<int[]>();

            arr.ProcessInBatches(batch => batches.Add(batch), batchSize: 3);

            batches.SelectMany(b => b).Should().Equal(arr);
            batches.Select(b => b.Length).Should().Equal(new[] { 3, 3, 3, 1 });
        }
        #endregion

        #region List Extensions
        [Fact]
        public void AddRange_Should_Add_All_Items()
        {
            var list = new List<int> { 1 };
            list.AddRange(2, 3, 4);
            list.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void RemoveWhere_Should_Remove_Matching_Items()
        {
            var list = new List<int> { 1, 2, 3, 4, 5 };
            var removed = list.RemoveWhere(x => x % 2 == 0);
            removed.Should().Be(2);
            list.Should().Equal(1, 3, 5);
        }

        [Fact]
        public void FastContains_Should_Be_Correct_For_Small_And_Large_Lists()
        {
            var small = new List<int> { 1, 2, 3 };
            small.FastContains(2).Should().BeTrue();
            small.FastContains(9).Should().BeFalse();

            var large = Enumerable.Range(1, 100).ToList();
            large.FastContains(50).Should().BeTrue();
            large.FastContains(101).Should().BeFalse();
        }

        [Fact]
        public void Partition_Should_Yield_Partitions_Of_Given_Size()
        {
            var list = Enumerable.Range(1, 10).ToList();
            var parts = list.Partition(4).ToList();

            parts.Count.Should().Be(3);
            parts[0].Should().Equal(1, 2, 3, 4);
            parts[1].Should().Equal(5, 6, 7, 8);
            parts[2].Should().Equal(9, 10);
        }

        [Fact]
        public void Partition_Should_Throw_On_Invalid_Size()
        {
            var list = new List<int> { 1 };
            Action act = () => list.Partition(0).ToList();
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ProcessParallel_Should_Process_All_Items()
        {
            var list = Enumerable.Range(1, 50).ToList();
            var bag = new ConcurrentBag<int>();

            list.ProcessParallel(i => bag.Add(i), maxDegreeOfParallelism: 1);

            bag.OrderBy(x => x).Should().Equal(list);
        }
        #endregion

        #region Dictionary Extensions
        [Fact]
        public void GetOrAdd_Should_Add_When_Missing_And_Return_Existing_When_Present()
        {
            var dict = new Dictionary<string, int>();
            var created = dict.GetOrAdd("a", _ => 10);
            created.Should().Be(10);

            var existing = dict.GetOrAdd("a", _ => 20);
            existing.Should().Be(10);
            dict["a"].Should().Be(10);
        }

        [Fact]
        public void MergeFrom_Should_Merge_With_Overwrite_Toggle()
        {
            var a = new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 };
            var b = new Dictionary<string, int> { ["y"] = 9, ["z"] = 3 };

            a.MergeFrom(b, overwriteExisting: false);
            a.Should().Equal(new Dictionary<string, int> { ["x"] = 1, ["y"] = 2, ["z"] = 3 });

            a.MergeFrom(new Dictionary<string, int> { ["y"] = 7 }, overwriteExisting: true);
            a["y"].Should().Be(7);
        }

        [Fact]
        public void GetMany_Should_Return_Only_Existing_Keys()
        {
            var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            var res = dict.GetMany(new[] { "a", "c" });
            res.Should().Equal(new Dictionary<string, int> { ["a"] = 1 });
        }
        #endregion

        #region ConcurrentDictionary Extensions
        [Fact]
        public void UpdateMany_Should_Upsert_All_Pairs()
        {
            var dict = new ConcurrentDictionary<string, int>();
            var updates = new[]
            {
                new KeyValuePair<string,int>("a", 1),
                new KeyValuePair<string,int>("b", 2),
                new KeyValuePair<string,int>("a", 3)
            };

            dict.UpdateMany(updates);
            dict["a"].Should().Be(3);
            dict["b"].Should().Be(2);
        }

        [Fact]
        public void ClearWhere_Should_Remove_All_When_No_Predicate()
        {
            var dict = new ConcurrentDictionary<int, int>(Enumerable.Range(1, 5).Select(i => new KeyValuePair<int,int>(i, i)));
            var removed = dict.ClearWhere();
            removed.Should().Be(5);
            dict.Should().BeEmpty();
        }

        [Fact]
        public void ClearWhere_Should_Remove_By_Predicate()
        {
            var dict = new ConcurrentDictionary<int, int>(Enumerable.Range(1, 6).Select(i => new KeyValuePair<int,int>(i, i)));
            var removed = dict.ClearWhere(kvp => kvp.Key % 2 == 0);
            removed.Should().Be(3);
            dict.Keys.Should().OnlyContain(k => k % 2 == 1);
        }
        #endregion

        #region HashSet Extensions
        [Fact]
        public void HasIntersection_Should_Return_Correct_Result()
        {
            var hs = new HashSet<int> { 1, 2, 3 };
            hs.HasIntersection(new[] { 4, 5, 6 }).Should().BeFalse();
            hs.HasIntersection(new[] { 0, 2 }).Should().BeTrue();
        }

        [Fact]
        public void AddMany_Should_Return_Count_Of_Newly_Added()
        {
            var hs = new HashSet<int> { 1 };
            var added = hs.AddMany(new[] { 1, 2, 3 });
            added.Should().Be(2);
            hs.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }
        #endregion

        #region IEnumerable Extensions
        [Fact]
        public void ChunkOptimal_With_Custom_Size_Should_Chunk_Correctly()
        {
            var items = Enumerable.Range(1, 10);
            var chunks = items.ChunkOptimal(3).ToList();
            chunks.Should().HaveCount(4);
            chunks[0].Should().Equal(1,2,3);
            chunks[3].Should().Equal(10);
        }

        [Fact]
        public void ChunkOptimal_Default_Should_Yield_Single_Chunk_For_Small_Source()
        {
            var items = Enumerable.Range(1, 10);
            var chunks = items.ChunkOptimal().ToList();
            chunks.Should().HaveCount(1);
            chunks[0].Should().Equal(items);
        }

        [Fact]
        public void SafeParallelSelect_Should_Collect_Results_And_Ignore_Errors()
        {
            var source = new[] { 1, 2, 3, 4, 5 };
            var errors = 0;

            var results = source.SafeParallelSelect(i =>
            {
                if (i % 2 == 0) throw new InvalidOperationException();
                return i * 10;
            }, ex => Interlocked.Increment(ref errors));

            results.OrderBy(x => x).Should().Equal(10, 30, 50);
            errors.Should().Be(2);
        }

        [Fact]
        public async Task ProcessInBatchesAsync_Should_Honor_MaxConcurrency_And_Process_All()
        {
            var items = Enumerable.Range(1, 20).ToList();
            var processed = new ConcurrentBag<int>();
            int current = 0; int maxObserved = 0;

            await items.ProcessInBatchesAsync(async batch =>
            {
                Interlocked.Increment(ref current);
                maxObserved = Math.Max(maxObserved, current);
                await Task.Delay(10);
                foreach (var i in batch) processed.Add(i);
                Interlocked.Decrement(ref current);
            }, batchSize: 5, maxConcurrency: 2);

            processed.OrderBy(x => x).Should().Equal(items);
            maxObserved.Should().BeLessOrEqualTo(2);
        }
        #endregion

        #region Performance Monitoring
        [Fact]
        public void MeasureEnumeration_Should_Return_Results_With_NonNegative_Metrics()
        {
            var src = Enumerable.Range(1, 1000);
            var (results, duration, memory) = src.MeasureEnumeration();
            results.Should().Equal(src);
            duration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
            memory.Should().BeGreaterOrEqualTo(0);
        }
        #endregion

        #region Performance Analyzer
        [Fact]
        public void CollectionPerformanceAnalyzer_Should_Record_And_Clear_Metrics()
        {
            CollectionPerformanceAnalyzer.ClearMetrics();
            CollectionPerformanceAnalyzer.RecordOperation("OpA", 10, TimeSpan.FromMilliseconds(5));
            CollectionPerformanceAnalyzer.RecordOperation("OpA", 20, TimeSpan.FromMilliseconds(10));

            var metrics = CollectionPerformanceAnalyzer.GetMetrics();
            metrics.Should().ContainKey("OpA");
            var snapshot = metrics["OpA"].GetSnapshot();
            snapshot.OperationCount.Should().Be(2);
            snapshot.TotalItems.Should().Be(30);
            snapshot.MinDuration.Should().BeLessThanOrEqualTo(snapshot.MaxDuration);

            CollectionPerformanceAnalyzer.ClearMetrics();
            CollectionPerformanceAnalyzer.GetMetrics().Should().BeEmpty();
        }

        [Fact]
        public void CollectionMetrics_Should_Aggregate_Stats_Correctly()
        {
            var metrics = new CollectionMetrics();
            metrics.RecordOperation(5, TimeSpan.FromTicks(10));
            metrics.RecordOperation(15, TimeSpan.FromTicks(30));
            var snap = metrics.GetSnapshot();

            snap.OperationCount.Should().Be(2);
            snap.TotalItems.Should().Be(20);
            snap.AverageDuration.Should().Be(TimeSpan.FromTicks(20));
            snap.MinDuration.Should().Be(TimeSpan.FromTicks(10));
            snap.MaxDuration.Should().Be(TimeSpan.FromTicks(30));
            snap.ItemsPerSecond.Should().BeGreaterThan(0);
        }
        #endregion
    }
}
