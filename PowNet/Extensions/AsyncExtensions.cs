using System.Collections.Concurrent;
using System.Diagnostics;
using PowNet.Services;
using Microsoft.Extensions.Caching.Memory;

namespace PowNet.Extensions
{
    /// <summary>
    /// High-performance async extensions optimized for .NET 10
    /// </summary>
    public static class AsyncExtensions
    {
        #region Task Extensions

        /// <summary>
        /// Execute tasks with controlled concurrency and timeout
        /// </summary>
        public static async Task<T[]> ExecuteWithConcurrency<T>(
            this IEnumerable<Func<Task<T>>> taskFactories,
            int maxConcurrency = -1,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (maxConcurrency <= 0)
                maxConcurrency = Environment.ProcessorCount;

            var semaphore = new SemaphoreSlim(maxConcurrency);
            var results = new ConcurrentBag<T>();
            var exceptions = new ConcurrentBag<Exception>();

            var cts = timeout.HasValue 
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (timeout.HasValue)
                cts!.CancelAfter(timeout.Value);

            try
            {
                var tasks = taskFactories.Select(async factory =>
                {
                    await semaphore.WaitAsync(cts?.Token ?? cancellationToken);
                    try
                    {
                        var result = await factory().ConfigureAwait(false);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }

                return results.ToArray();
            }
            finally
            {
                semaphore.Dispose();
                cts?.Dispose();
            }
        }

        /// <summary>
        /// Retry with exponential backoff and jitter
        /// </summary>
        public static async Task<T> RetryWithBackoff<T>(
            this Func<Task<T>> taskFactory,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            double backoffMultiplier = 2.0,
            TimeSpan? maxDelay = null,
            Func<Exception, bool>? retryCondition = null,
            CancellationToken cancellationToken = default)
        {
            var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
            var maxDelayValue = maxDelay ?? TimeSpan.FromSeconds(30);
            var random = new Random();

            Exception lastException = null!;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await taskFactory().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    lastException = ex;

                    if (retryCondition?.Invoke(ex) == false)
                        throw;

                    if (attempt > 0)
                    {
                        // Add jitter to prevent thundering herd
                        var jitter = random.NextDouble() * 0.1 + 0.9; // 90-100% of delay
                        var actualDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitter);
                        actualDelay = actualDelay > maxDelayValue ? maxDelayValue : actualDelay;

                        await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                    }

                    delay = TimeSpan.FromMilliseconds(Math.Min(
                        delay.TotalMilliseconds * backoffMultiplier,
                        maxDelayValue.TotalMilliseconds));
                }
            }

            throw lastException;
        }

        /// <summary>
        /// Timeout wrapper with proper cancellation
        /// </summary>
        public static async Task<T> WithTimeout<T>(
            this Task<T> task,
            TimeSpan timeout,
            string? operationName = null)
        {
            using var cts = new CancellationTokenSource(timeout);
            
            try
            {
                return await task.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"Operation '{operationName ?? "Unknown"}' timed out after {timeout.TotalSeconds:F1} seconds");
            }
        }

        /// <summary>
        /// Execute with circuit breaker pattern
        /// </summary>
        public static async Task<T> WithCircuitBreaker<T>(
            this Func<Task<T>> taskFactory,
            string circuitName,
            int failureThreshold = 5,
            TimeSpan? resetTimeout = null)
        {
            var breaker = CircuitBreakerManager.GetOrCreate(circuitName, failureThreshold, 
                resetTimeout ?? TimeSpan.FromMinutes(1));

            return await breaker.ExecuteAsync(taskFactory).ConfigureAwait(false);
        }

        #endregion

        #region Parallel Processing

        /// <summary>
        /// Process enumerable in parallel with controlled batching
        /// </summary>
        public static async Task<TResult[]> ProcessParallelAsync<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<TResult>> processor,
            int? maxConcurrency = null,
            int? batchSize = null,
            CancellationToken cancellationToken = default)
        {
            var items = source.ToArray();
            if (items.Length == 0) return [];

            var concurrency = maxConcurrency ?? Math.Min(Environment.ProcessorCount, items.Length);
            var actualBatchSize = batchSize ?? Math.Max(1, items.Length / concurrency);

            var semaphore = new SemaphoreSlim(concurrency);
            var results = new ConcurrentBag<TResult>();

            try
            {
                var tasks = items.Select(async item =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var result = await processor(item).ConfigureAwait(false);
                        results.Add(result);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
                return results.ToArray();
            }
            finally
            {
                semaphore.Dispose();
            }
        }

        /// <summary>
        /// Batch process with progress reporting
        /// </summary>
        public static async Task ProcessInBatchesAsync<T>(
            this IEnumerable<T> source,
            Func<IEnumerable<T>, Task> processor,
            int batchSize = 100,
            IProgress<BatchProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var items = source.ToArray();
            var totalBatches = (int)Math.Ceiling((double)items.Length / batchSize);
            var processedBatches = 0;

            for (int i = 0; i < items.Length; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = items.Skip(i).Take(batchSize);
                await processor(batch).ConfigureAwait(false);

                processedBatches++;
                progress?.Report(new BatchProgress
                {
                    ProcessedBatches = processedBatches,
                    TotalBatches = totalBatches,
                    ProcessedItems = Math.Min(i + batchSize, items.Length),
                    TotalItems = items.Length
                });
            }
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Measure async operation performance
        /// </summary>
        public static async Task<PerformanceResult<T>> MeasureAsync<T>(
            this Task<T> task,
            string? operationName = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            try
            {
                var result = await task.ConfigureAwait(false);
                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);

                return new PerformanceResult<T>
                {
                    Result = result,
                    Duration = stopwatch.Elapsed,
                    MemoryAllocated = Math.Max(0, memoryAfter - memoryBefore),
                    OperationName = operationName ?? "Unknown",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new PerformanceResult<T>
                {
                    Duration = stopwatch.Elapsed,
                    OperationName = operationName ?? "Unknown",
                    IsSuccess = false,
                    Exception = ex
                };
            }
        }

        /// <summary>
        /// Track async operation metrics
        /// </summary>
        public static async Task<T> TrackMetrics<T>(
            this Task<T> task,
            string operationName,
            Dictionary<string, object>? metadata = null)
        {
            var result = await task.MeasureAsync(operationName).ConfigureAwait(false);
            
            // Record metrics (could integrate with telemetry)
            AsyncMetricsCollector.Record(operationName, result.Duration, result.IsSuccess, metadata);
            
            if (result.IsSuccess)
                return result.Result;
            
            throw result.Exception!;
        }

        #endregion

        #region Caching Extensions

        /// <summary>
        /// Cache async operation result with expiration
        /// </summary>
        public static async Task<T> CacheAsync<T>(
            this Func<Task<T>> taskFactory,
            string cacheKey,
            TimeSpan? expiration = null,
            bool refreshInBackground = false)
        {
            var cache = MemoryService.SharedMemoryCache;
            
            // Properly detect presence even for value types
            if (cache.TryGetValue(cacheKey, out var existing) && existing is T cached)
            {
                return cached;
            }

            // Execute and cache result
            var result = await taskFactory().ConfigureAwait(false);
            cache.TryAdd(cacheKey, result, expiration);

            return result;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Progress information for batch processing
    /// </summary>
    public record BatchProgress
    {
        public int ProcessedBatches { get; init; }
        public int TotalBatches { get; init; }
        public int ProcessedItems { get; init; }
        public int TotalItems { get; init; }
        public double PercentComplete => TotalItems == 0 ? 0 : (double)ProcessedItems / TotalItems * 100;
    }

    /// <summary>
    /// Performance measurement result
    /// </summary>
    public record PerformanceResult<T>
    {
        public T Result { get; init; } = default!;
        public TimeSpan Duration { get; init; }
        public long MemoryAllocated { get; init; }
        public string OperationName { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public Exception? Exception { get; init; }
    }

    /// <summary>
    /// Circuit breaker implementation
    /// </summary>
    public class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private readonly object _lock = new();
        
        private int _failureCount = 0;
        private DateTime _nextAttemptTime = DateTime.MinValue;
        private CircuitState _state = CircuitState.Closed;

        public CircuitBreaker(int failureThreshold, TimeSpan resetTimeout)
        {
            _failureThreshold = failureThreshold;
            _resetTimeout = resetTimeout;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> taskFactory)
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open)
                {
                    if (DateTime.UtcNow < _nextAttemptTime)
                    {
                        throw new InvalidOperationException("Circuit breaker is open");
                    }
                    _state = CircuitState.HalfOpen;
                }
            }

            try
            {
                var result = await taskFactory().ConfigureAwait(false);
                
                lock (_lock)
                {
                    _failureCount = 0;
                    _state = CircuitState.Closed;
                }
                
                return result;
            }
            catch (Exception)
            {
                lock (_lock)
                {
                    _failureCount++;
                    if (_failureCount >= _failureThreshold)
                    {
                        _state = CircuitState.Open;
                        _nextAttemptTime = DateTime.UtcNow.Add(_resetTimeout);
                    }
                }
                throw;
            }
        }
    }

    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Manages circuit breakers by name
    /// </summary>
    public static class CircuitBreakerManager
    {
        private static readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new();

        public static CircuitBreaker GetOrCreate(string name, int failureThreshold, TimeSpan resetTimeout)
        {
            return _breakers.GetOrAdd(name, _ => new CircuitBreaker(failureThreshold, resetTimeout));
        }

        public static void Remove(string name)
        {
            _breakers.TryRemove(name, out _);
        }

        public static void Clear()
        {
            _breakers.Clear();
        }
    }

    /// <summary>
    /// Collects metrics for async operations
    /// </summary>
    public static class AsyncMetricsCollector
    {
        private static readonly ConcurrentDictionary<string, AsyncOperationMetrics> _metrics = new();

        public static void Record(string operationName, TimeSpan duration, bool success, 
            Dictionary<string, object>? metadata = null)
        {
            var metrics = _metrics.GetOrAdd(operationName, _ => new AsyncOperationMetrics());
            metrics.Record(duration, success, metadata);
        }

        public static Dictionary<string, AsyncOperationMetrics> GetMetrics()
        {
            return new Dictionary<string, AsyncOperationMetrics>(_metrics);
        }

        public static void ClearMetrics()
        {
            _metrics.Clear();
        }
    }

    public class AsyncOperationMetrics
    {
        private readonly object _lock = new();
        private long _totalOperations = 0;
        private long _successfulOperations = 0;
        private long _totalDurationTicks = 0;
        private long _minDurationTicks = long.MaxValue;
        private long _maxDurationTicks = long.MinValue;

        public void Record(TimeSpan duration, bool success, Dictionary<string, object>? metadata = null)
        {
            lock (_lock)
            {
                _totalOperations++;
                if (success) _successfulOperations++;
                
                _totalDurationTicks += duration.Ticks;
                _minDurationTicks = Math.Min(_minDurationTicks, duration.Ticks);
                _maxDurationTicks = Math.Max(_maxDurationTicks, duration.Ticks);
            }
        }

        public AsyncMetricsSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new AsyncMetricsSnapshot
                {
                    TotalOperations = _totalOperations,
                    SuccessfulOperations = _successfulOperations,
                    SuccessRate = _totalOperations == 0 ? 0 : (double)_successfulOperations / _totalOperations,
                    AverageDuration = _totalOperations == 0 ? TimeSpan.Zero : 
                        new TimeSpan(_totalDurationTicks / _totalOperations),
                    MinDuration = _minDurationTicks == long.MaxValue ? TimeSpan.Zero : 
                        new TimeSpan(_minDurationTicks),
                    MaxDuration = _maxDurationTicks == long.MinValue ? TimeSpan.Zero : 
                        new TimeSpan(_maxDurationTicks)
                };
            }
        }
    }

    public record AsyncMetricsSnapshot
    {
        public long TotalOperations { get; init; }
        public long SuccessfulOperations { get; init; }
        public double SuccessRate { get; init; }
        public TimeSpan AverageDuration { get; init; }
        public TimeSpan MinDuration { get; init; }
        public TimeSpan MaxDuration { get; init; }
    }

    #endregion
}