using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PowNet.Logging;

namespace PowNet.Extensions
{
    /// <summary>
    /// Advanced performance optimization extensions for PowNet framework
    /// </summary>
    public static class PerformanceExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("Performance");

        #region Caching Extensions

        /// <summary>
        /// Cache result of expensive function call
        /// </summary>
        public static Func<TResult> Memoize<TResult>(this Func<TResult> func)
        {
            var cache = new Lazy<TResult>(func);
            return () => cache.Value;
        }

        /// <summary>
        /// Cache result of expensive function call with parameter
        /// </summary>
        public static Func<TParam, TResult> Memoize<TParam, TResult>(this Func<TParam, TResult> func) 
            where TParam : notnull
        {
            var cache = new ConcurrentDictionary<TParam, TResult>();
            return param => cache.GetOrAdd(param, func);
        }

        /// <summary>
        /// Cache result with expiration
        /// </summary>
        public static Func<TResult> MemoizeWithExpiration<TResult>(this Func<TResult> func, TimeSpan expiration)
        {
            var cache = new TimedCache<TResult>();
            return () => cache.GetOrAdd("default", func, expiration);
        }

        /// <summary>
        /// Cache async result
        /// </summary>
        public static Func<Task<TResult>> MemoizeAsync<TResult>(this Func<Task<TResult>> func)
        {
            var cache = new Lazy<Task<TResult>>(func);
            return () => cache.Value;
        }

        #endregion

        #region Batch Processing Extensions

        /// <summary>
        /// Process items in batches for better performance
        /// </summary>
        public static async Task ProcessInBatchesAsync<T>(
            this IEnumerable<T> source,
            Func<IEnumerable<T>, Task> processor,
            int batchSize = 100,
            int maxConcurrency = 4,
            CancellationToken cancellationToken = default)
        {
            var batches = source.Chunk(batchSize);
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = batches.Select(async batch =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await processor(batch);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Transform items in batches
        /// </summary>
        public static async Task<IEnumerable<TResult>> TransformInBatchesAsync<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<TResult>> transformer,
            int batchSize = 100,
            int maxConcurrency = 4)
        {
            var results = new ConcurrentBag<TResult>();
            
            await source.ProcessInBatchesAsync(async batch =>
            {
                var batchTasks = batch.Select(async item =>
                {
                    var result = await transformer(item);
                    results.Add(result);
                });
                await Task.WhenAll(batchTasks);
            }, batchSize, maxConcurrency);

            return results;
        }

        #endregion

        #region Lazy Loading Extensions

        /// <summary>
        /// Create lazy loader with thread safety
        /// </summary>
        public static Lazy<T> CreateLazy<T>(Func<T> factory, bool isThreadSafe = true)
        {
            return isThreadSafe 
                ? new Lazy<T>(factory, LazyThreadSafetyMode.ExecutionAndPublication)
                : new Lazy<T>(factory, LazyThreadSafetyMode.None);
        }

        /// <summary>
        /// Create async lazy loader
        /// </summary>
        public static AsyncLazy<T> CreateAsyncLazy<T>(Func<Task<T>> factory)
        {
            return new AsyncLazy<T>(factory);
        }

        #endregion

        #region Pipeline Processing Extensions

        /// <summary>
        /// Create processing pipeline with multiple stages
        /// </summary>
        public static ProcessingPipeline<T> CreatePipeline<T>(this IEnumerable<T> source)
        {
            return new ProcessingPipeline<T>(source);
        }

        /// <summary>
        /// Add stage to processing pipeline
        /// </summary>
        public static ProcessingPipeline<TOut> AddStage<TIn, TOut>(
            this ProcessingPipeline<TIn> pipeline,
            Func<TIn, TOut> processor)
        {
            return pipeline.Transform(processor);
        }

        /// <summary>
        /// Add async stage to processing pipeline
        /// </summary>
        public static ProcessingPipeline<TOut> AddAsyncStage<TIn, TOut>(
            this ProcessingPipeline<TIn> pipeline,
            Func<TIn, Task<TOut>> processor)
        {
            return pipeline.TransformAsync(processor);
        }

        #endregion

        #region Memory Optimization Extensions

        /// <summary>
        /// Process large enumerable without loading all into memory
        /// </summary>
        public static IEnumerable<TResult> ProcessLarge<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, TResult> processor,
            int bufferSize = 1000)
        {
            using var enumerator = source.GetEnumerator();
            var buffer = new List<TSource>(bufferSize);

            while (enumerator.MoveNext())
            {
                buffer.Add(enumerator.Current);
                
                if (buffer.Count >= bufferSize)
                {
                    foreach (var item in buffer)
                    {
                        yield return processor(item);
                    }
                    buffer.Clear();
                }
            }

            // Process remaining items
            foreach (var item in buffer)
            {
                yield return processor(item);
            }
        }

        /// <summary>
        /// Force garbage collection and return memory freed
        /// </summary>
        public static long ForceGarbageCollection()
        {
            var memoryBefore = GC.GetTotalMemory(false);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryFreed = memoryBefore - memoryAfter;
            
            _logger.LogDebug("Garbage collection freed {MemoryFreed} bytes ({MemoryFreedMB} MB)", 
                memoryFreed, memoryFreed / 1024 / 1024);
            
            return memoryFreed;
        }

        #endregion

        #region Retry Extensions

        /// <summary>
        /// Retry operation with exponential backoff
        /// </summary>
        public static async Task<T> RetryAsync<T>(
            this Func<Task<T>> operation,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            double backoffMultiplier = 2.0,
            Func<Exception, bool>? shouldRetry = null)
        {
            var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
            Exception lastException = null!;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;

                    if (shouldRetry?.Invoke(ex) == false)
                        throw;

                    _logger.LogWarning("Operation failed on attempt {Attempt}, retrying in {Delay}ms: {Error}", 
                        attempt + 1, delay.TotalMilliseconds, ex.Message);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * backoffMultiplier);
                }
            }

            throw lastException;
        }

        /// <summary>
        /// Retry synchronous operation
        /// </summary>
        public static T Retry<T>(
            this Func<T> operation,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            double backoffMultiplier = 2.0,
            Func<Exception, bool>? shouldRetry = null)
        {
            var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
            Exception lastException = null!;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;

                    if (shouldRetry?.Invoke(ex) == false)
                        throw;

                    _logger.LogWarning("Operation failed on attempt {Attempt}, retrying in {Delay}ms: {Error}", 
                        attempt + 1, delay.TotalMilliseconds, ex.Message);

                    Thread.Sleep(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * backoffMultiplier);
                }
            }

            throw lastException;
        }

        #endregion

        #region Monitoring Extensions

        /// <summary>
        /// Monitor method performance automatically
        /// </summary>
        public static T MonitorPerformance<T>(
            this Func<T> func,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            bool logSlowOperations = true,
            TimeSpan? slowThreshold = null)
        {
            var operationName = $"{Path.GetFileNameWithoutExtension(filePath)}.{methodName}:{lineNumber}";
            var threshold = slowThreshold ?? TimeSpan.FromMilliseconds(1000);

            using var measurement = Diagnostics.DiagnosticsManager.MeasurePerformance(operationName);
            var sw = Stopwatch.StartNew();

            try
            {
                var result = func();
                sw.Stop();

                if (logSlowOperations && sw.Elapsed > threshold)
                {
                    _logger.LogWarning("Slow operation detected: {Operation} took {Duration}ms (threshold: {Threshold}ms)",
                        operationName, sw.ElapsedMilliseconds, threshold.TotalMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError("Operation {Operation} failed after {Duration}ms: {Error}",
                    operationName, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Monitor async method performance
        /// </summary>
        public static async Task<T> MonitorPerformanceAsync<T>(
            this Func<Task<T>> func,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            bool logSlowOperations = true,
            TimeSpan? slowThreshold = null)
        {
            var operationName = $"{Path.GetFileNameWithoutExtension(filePath)}.{methodName}:{lineNumber}";
            var threshold = slowThreshold ?? TimeSpan.FromMilliseconds(2000);

            using var measurement = Diagnostics.DiagnosticsManager.MeasurePerformance(operationName);
            var sw = Stopwatch.StartNew();

            try
            {
                var result = await func();
                sw.Stop();

                if (logSlowOperations && sw.Elapsed > threshold)
                {
                    _logger.LogWarning("Slow async operation detected: {Operation} took {Duration}ms (threshold: {Threshold}ms)",
                        operationName, sw.ElapsedMilliseconds, threshold.TotalMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError("Async operation {Operation} failed after {Duration}ms: {Error}",
                    operationName, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Thread-safe cache with expiration based on monotonic clock to avoid system clock granularity issues.
    /// </summary>
    public class TimedCache<T>
    {
        private readonly ConcurrentDictionary<string, CacheItem<T>> _cache = new();

        public T GetOrAdd(string key, Func<T> factory, TimeSpan expiration)
        {
            var nowTick = Stopwatch.GetTimestamp();
            var expiryTicks = nowTick + (long)(expiration.TotalSeconds * Stopwatch.Frequency);

            if (_cache.TryGetValue(key, out var item) && item.MonotonicExpiresAtTick > nowTick)
            {
                return item.Value;
            }

            var newValue = factory();
            var newItem = new CacheItem<T>(newValue, expiryTicks);
            _cache.AddOrUpdate(key, newItem, (k, old) => newItem);

            // periodic cleanup
            if (_cache.Count > 100)
            {
                CleanExpiredItems();
            }

            return newValue;
        }

        private void CleanExpiredItems()
        {
            var nowTick = Stopwatch.GetTimestamp();
            var expiredKeys = _cache.Where(kvp => kvp.Value.MonotonicExpiresAtTick <= nowTick)
                                     .Select(kvp => kvp.Key)
                                     .ToList();
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    public class CacheItem<T>
    {
        public CacheItem(T value, long monotonicExpiresAtTick)
        {
            Value = value;
            MonotonicExpiresAtTick = monotonicExpiresAtTick;
        }
        public T Value { get; }
        public long MonotonicExpiresAtTick { get; }
    }

    /// <summary>
    /// Async lazy implementation
    /// </summary>
    public class AsyncLazy<T>
    {
        private readonly Lazy<Task<T>> _lazy;

        public AsyncLazy(Func<Task<T>> taskFactory)
        {
            _lazy = new Lazy<Task<T>>(taskFactory);
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            return _lazy.Value.GetAwaiter();
        }

        public Task<T> Value => _lazy.Value;
    }

    /// <summary>
    /// Processing pipeline for efficient data transformation
    /// </summary>
    public class ProcessingPipeline<T>
    {
        private readonly IEnumerable<T> _source;

        public ProcessingPipeline(IEnumerable<T> source)
        {
            _source = source;
        }

        public ProcessingPipeline<TOut> Transform<TOut>(Func<T, TOut> transformer)
        {
            return new ProcessingPipeline<TOut>(_source.Select(transformer));
        }

        public ProcessingPipeline<TOut> TransformAsync<TOut>(Func<T, Task<TOut>> transformer)
        {
            var transformed = TransformAsyncEnumerable(transformer).GetAwaiter().GetResult();
            return new ProcessingPipeline<TOut>(transformed);
        }

        private async Task<IEnumerable<TOut>> TransformAsyncEnumerable<TOut>(Func<T, Task<TOut>> transformer)
        {
            var results = new List<TOut>();
            foreach (var item in _source)
            {
                var result = await transformer(item);
                results.Add(result);
            }
            return results;
        }

        public ProcessingPipeline<T> Filter(Func<T, bool> predicate)
        {
            return new ProcessingPipeline<T>(_source.Where(predicate));
        }

        public ProcessingPipeline<T> Take(int count)
        {
            return new ProcessingPipeline<T>(_source.Take(count));
        }

        public ProcessingPipeline<T> Skip(int count)
        {
            return new ProcessingPipeline<T>(_source.Skip(count));
        }

        public async Task<List<T>> ToListAsync()
        {
            if (_source is IAsyncEnumerable<T> asyncSource)
            {
                var result = new List<T>();
                await foreach (var item in asyncSource)
                {
                    result.Add(item);
                }
                return result;
            }

            return _source.ToList();
        }

        public T[] ToArray()
        {
            return _source.ToArray();
        }

        public IEnumerable<T> AsEnumerable()
        {
            return _source;
        }
    }

    #endregion
}