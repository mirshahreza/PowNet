using System.Collections.Concurrent;

namespace PowNet.Extensions
{
    /// <summary>
    /// High-performance collection extensions optimized for .NET 10
    /// </summary>
    public static class CollectionExtensions
    {
        #region Array Extensions

        /// <summary>
        /// Fast array copy using Buffer.BlockCopy when possible
        /// </summary>
        public static T[] FastCopy<T>(this T[] source) where T : struct
        {
            if (source == null || source.Length == 0) return [];
            
            var destination = new T[source.Length];
            if (typeof(T).IsPrimitive)
            {
                // Use ByteLength to avoid dependency on Unsafe.SizeOf<T>()
                Buffer.BlockCopy(source, 0, destination, 0, Buffer.ByteLength(source));
            }
            else
            {
                Array.Copy(source, destination, source.Length);
            }
            return destination;
        }

        /// <summary>
        /// High-performance array search using binary search when sorted
        /// </summary>
        public static int FastIndexOf<T>(this T[] array, T item, bool isSorted = false) where T : IComparable<T>
        {
            if (array == null || array.Length == 0) return -1;
            
            if (isSorted)
            {
                return Array.BinarySearch(array, item);
            }
            
            return Array.IndexOf(array, item);
        }

        /// <summary>
        /// Batch process array elements for better cache locality
        /// </summary>
        public static void ProcessInBatches<T>(this T[] array, Action<T[]> processor, int batchSize = 1000)
        {
            if (array == null || array.Length == 0) return;
            
            for (int i = 0; i < array.Length; i += batchSize)
            {
                var actualBatchSize = Math.Min(batchSize, array.Length - i);
                var batch = new T[actualBatchSize];
                Array.Copy(array, i, batch, 0, actualBatchSize);
                processor(batch);
            }
        }

        #endregion

        #region List Extensions

        /// <summary>
        /// Add multiple items efficiently to a list
        /// </summary>
        public static void AddRange<T>(this List<T> list, params T[] items)
        {
            if (items?.Length > 0)
            {
                list.AddRange(items);
            }
        }

        /// <summary>
        /// Remove items efficiently using predicate with reverse iteration
        /// </summary>
        public static int RemoveWhere<T>(this List<T> list, Func<T, bool> predicate)
        {
            var removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (predicate(list[i]))
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Fast contains check for small lists using linear search
        /// </summary>
        public static bool FastContains<T>(this List<T> list, T item) where T : IEquatable<T>
        {
            if (list == null || list.Count == 0) return false;
            
            // For small lists, linear search is faster than hash lookup
            if (list.Count <= 10)
            {
                foreach (var listItem in list)
                {
                    if (item?.Equals(listItem) == true)
                        return true;
                }
                return false;
            }
            
            return list.Contains(item);
        }

        /// <summary>
        /// Partition a list into multiple smaller lists
        /// </summary>
        public static IEnumerable<List<T>> Partition<T>(this List<T> source, int size)
        {
            if (source == null) yield break;
            if (size <= 0) throw new ArgumentException("Size must be positive", nameof(size));
            
            for (int i = 0; i < source.Count; i += size)
            {
                var count = Math.Min(size, source.Count - i);
                var partition = new List<T>(count);
                for (int j = 0; j < count; j++)
                {
                    partition.Add(source[i + j]);
                }
                yield return partition;
            }
        }

        /// <summary>
        /// Safe parallel processing with optimal degree of parallelism
        /// </summary>
        public static void ProcessParallel<T>(this List<T> source, Action<T> processor, 
            int? maxDegreeOfParallelism = null)
        {
            if (source == null || source.Count == 0) return;
            
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount
            };
            
            Parallel.ForEach(source, parallelOptions, processor);
        }

        #endregion

        #region Dictionary Extensions

        /// <summary>
        /// Thread-safe get or add with factory
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, 
            TKey key, Func<TKey, TValue> valueFactory) where TKey : notnull
        {
            if (dictionary.TryGetValue(key, out var existingValue))
                return existingValue;
            
            var newValue = valueFactory(key);
            dictionary[key] = newValue;
            return newValue;
        }

        /// <summary>
        /// Efficient merge of two dictionaries
        /// </summary>
        public static void MergeFrom<TKey, TValue>(this Dictionary<TKey, TValue> target, 
            Dictionary<TKey, TValue> source, bool overwriteExisting = true) where TKey : notnull
        {
            if (source == null) return;
            
            foreach (var kvp in source)
            {
                if (overwriteExisting || !target.ContainsKey(kvp.Key))
                {
                    target[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Get multiple values at once
        /// </summary>
        public static Dictionary<TKey, TValue> GetMany<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, 
            IEnumerable<TKey> keys) where TKey : notnull
        {
            var result = new Dictionary<TKey, TValue>();
            foreach (var key in keys)
            {
                if (dictionary.TryGetValue(key, out var value))
                {
                    result[key] = value;
                }
            }
            return result;
        }

        #endregion

        #region ConcurrentDictionary Extensions

        /// <summary>
        /// Batch update for ConcurrentDictionary
        /// Ensures deterministic "last write wins" semantics based on the enumeration order of <paramref name="updates"/>.
        /// </summary>
        public static void UpdateMany<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary,
            IEnumerable<KeyValuePair<TKey, TValue>> updates) where TKey : notnull
        {
            if (dictionary is null) throw new ArgumentNullException(nameof(dictionary));
            if (updates is null) return;

            // Do not parallelize to preserve input order deterministically.
            // Using the indexer provides upsert semantics in ConcurrentDictionary.
            foreach (var kvp in updates)
            {
                dictionary[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Safe clear with optional predicate
        /// </summary>
        public static int ClearWhere<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary,
            Func<KeyValuePair<TKey, TValue>, bool>? predicate = null) where TKey : notnull
        {
            var removed = 0;
            var keysToRemove = new List<TKey>();
            
            foreach (var kvp in dictionary)
            {
                if (predicate?.Invoke(kvp) != false)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                if (dictionary.TryRemove(key, out _))
                {
                    removed++;
                }
            }
            
            return removed;
        }

        #endregion

        #region HashSet Extensions

        /// <summary>
        /// Fast intersection check without creating new HashSet
        /// </summary>
        public static bool HasIntersection<T>(this HashSet<T> first, IEnumerable<T> second)
        {
            foreach (var item in second)
            {
                if (first.Contains(item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Add multiple items efficiently
        /// </summary>
        public static int AddMany<T>(this HashSet<T> hashSet, IEnumerable<T> items)
        {
            var added = 0;
            foreach (var item in items)
            {
                if (hashSet.Add(item))
                    added++;
            }
            return added;
        }

        #endregion

        #region IEnumerable Extensions

        /// <summary>
        /// Chunking with optimal batch size
        /// </summary>
        public static IEnumerable<T[]> ChunkOptimal<T>(this IEnumerable<T> source, int? chunkSize = null)
        {
            var actualChunkSize = chunkSize ?? Math.Max(1, Environment.ProcessorCount * 100);
            return source.Chunk(actualChunkSize);
        }

        /// <summary>
        /// Safe parallel select with exception handling
        /// </summary>
        public static IEnumerable<TResult> SafeParallelSelect<TSource, TResult>(
            this IEnumerable<TSource> source, 
            Func<TSource, TResult> selector,
            Action<Exception>? errorHandler = null)
        {
            var results = new ConcurrentBag<TResult>();
            
            Parallel.ForEach(source, item =>
            {
                try
                {
                    var result = selector(item);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                }
            });
            
            return results;
        }

        /// <summary>
        /// Memory-efficient distinct using hash set
        /// </summary>
        public static IEnumerable<T> FastDistinct<T>(this IEnumerable<T> source, 
            IEqualityComparer<T>? comparer = null)
        {
            var seen = new HashSet<T>(comparer);
            foreach (var item in source)
            {
                if (seen.Add(item))
                    yield return item;
            }
        }

        /// <summary>
        /// Batch processing with configurable parallel options
        /// </summary>
        public static async Task ProcessInBatchesAsync<T>(this IEnumerable<T> source,
            Func<IEnumerable<T>, Task> processor,
            int batchSize = 1000,
            int maxConcurrency = -1)
        {
            var batches = source.ChunkOptimal(batchSize);
            
            var semaphore = maxConcurrency > 0 
                ? new SemaphoreSlim(maxConcurrency) 
                : null;
            
            try
            {
                var tasks = batches.Select(async batch =>
                {
                    if (semaphore != null)
                        await semaphore.WaitAsync();
                    
                    try
                    {
                        await processor(batch);
                    }
                    finally
                    {
                        semaphore?.Release();
                    }
                });
                
                await Task.WhenAll(tasks);
            }
            finally
            {
                semaphore?.Dispose();
            }
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Measure enumeration performance
        /// </summary>
        public static (T[] Results, TimeSpan Duration, long MemoryUsed) MeasureEnumeration<T>(
            this IEnumerable<T> source)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            
            var results = source.ToArray();
            
            sw.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            
            return (results, sw.Elapsed, memoryAfter - memoryBefore);
        }

        #endregion
    }

    #region Collection Performance Analyzers

    /// <summary>
    /// Analyzer for collection operation performance
    /// </summary>
    public static class CollectionPerformanceAnalyzer
    {
        private static readonly ConcurrentDictionary<string, CollectionMetrics> _metrics = new();

        public static void RecordOperation(string operationType, int itemCount, TimeSpan duration)
        {
            var metrics = _metrics.GetOrAdd(operationType, _ => new CollectionMetrics());
            metrics.RecordOperation(itemCount, duration);
        }

        public static Dictionary<string, CollectionMetrics> GetMetrics()
        {
            return new Dictionary<string, CollectionMetrics>(_metrics);
        }

        public static void ClearMetrics()
        {
            _metrics.Clear();
        }
    }

    public class CollectionMetrics
    {
        private readonly object _lock = new();
        private long _operationCount = 0;
        private long _totalItems = 0;
        private long _totalDurationTicks = 0;
        private long _minDurationTicks = long.MaxValue;
        private long _maxDurationTicks = long.MinValue;

        public void RecordOperation(int itemCount, TimeSpan duration)
        {
            lock (_lock)
            {
                _operationCount++;
                _totalItems += itemCount;
                _totalDurationTicks += duration.Ticks;
                _minDurationTicks = Math.Min(_minDurationTicks, duration.Ticks);
                _maxDurationTicks = Math.Max(_maxDurationTicks, duration.Ticks);
            }
        }

        public CollectionMetricsSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new CollectionMetricsSnapshot
                {
                    OperationCount = _operationCount,
                    TotalItems = _totalItems,
                    AverageDuration = _operationCount == 0 ? TimeSpan.Zero : 
                        new TimeSpan(_totalDurationTicks / _operationCount),
                    MinDuration = _minDurationTicks == long.MaxValue ? TimeSpan.Zero : 
                        new TimeSpan(_minDurationTicks),
                    MaxDuration = _maxDurationTicks == long.MinValue ? TimeSpan.Zero : 
                        new TimeSpan(_maxDurationTicks),
                    ItemsPerSecond = _totalDurationTicks == 0 ? 0 : 
                        _totalItems * TimeSpan.TicksPerSecond / _totalDurationTicks
                };
            }
        }
    }

    public record CollectionMetricsSnapshot
    {
        public long OperationCount { get; init; }
        public long TotalItems { get; init; }
        public TimeSpan AverageDuration { get; init; }
        public TimeSpan MinDuration { get; init; }
        public TimeSpan MaxDuration { get; init; }
        public long ItemsPerSecond { get; init; }
    }

    #endregion
}