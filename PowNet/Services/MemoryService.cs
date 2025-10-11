using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.Caching.Memory;

namespace PowNet.Services
{
    /// <summary>
    /// Enhanced memory service with performance monitoring and advanced caching capabilities
    /// </summary>
    public static class MemoryService
    {
        // Removed fragile reflection-based key access; we rely on our own tracking.
        // private static readonly Lazy<Func<MemoryCache, object>> GetCoherentState = new(() => CreateGetter<MemoryCache, object>(typeof(MemoryCache).GetField("_coherentState", BindingFlags.NonPublic | BindingFlags.Instance)!));
        // private static readonly Lazy<Func<object, IDictionary>> GetEntriesLocal = new(() => CreateGetter<object, IDictionary>(typeof(MemoryCache).GetNestedType("CoherentState", BindingFlags.NonPublic)!.GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!));

        // Performance monitoring
        private static readonly ConcurrentDictionary<string, CacheStatistics> _cacheStats = new();
        private static long _totalHits = 0;
        private static long _totalMisses = 0;
        private static long _totalSets = 0;
        private static long _totalRemovals = 0;

        // Background cleanup
        private static readonly Timer _cleanupTimer;
        private static readonly ConcurrentDictionary<string, DateTime> _keyAccessTimes = new();

        static MemoryService()
        {
            // Initialize cleanup timer - runs every 5 minutes
            _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private static Func<TParam, TReturn> CreateGetter<TParam, TReturn>(FieldInfo field)
        {
            var methodName = $"{field.ReflectedType?.FullName}.get_{field.Name}";
            var method = new DynamicMethod(methodName, typeof(TReturn), [typeof(TParam)], typeof(TParam), true);
            var ilGen = method.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, field);
            ilGen.Emit(OpCodes.Ret);
            return (Func<TParam, TReturn>)method.CreateDelegate(typeof(Func<TParam, TReturn>));
        }

        // private static readonly Func<MemoryCache, IDictionary> GetEntries = cache => GetEntriesLocal.Value(GetCoherentState.Value(cache));

        // Return keys tracked by MemoryService instead of using reflection on MemoryCache internals
        public static ICollection GetKeys(this IMemoryCache memoryCache)
        {
            // Return a snapshot as an array to implement ICollection
            return _keyAccessTimes.Keys.ToArray();
        }

        public static IEnumerable<T> GetKeys<T>(this IMemoryCache memoryCache) => memoryCache.GetKeys().OfType<T>();

        /// <summary>
        /// Enhanced method with performance tracking
        /// </summary>
        public static List<string> GetKeysStartsWith(this IMemoryCache memoryCache, string startingWith)
        {
            var res = new List<string>();
            var startingWithSpan = startingWith.AsSpan();
            foreach (var key in _keyAccessTimes.Keys)
            {
                if (key.AsSpan().StartsWith(startingWithSpan, StringComparison.Ordinal))
                {
                    res.Add(key);
                }
            }
            return res;
        }

        /// <summary>
        /// Enhanced TryAdd with expiration and statistics tracking
        /// </summary>
        public static void TryAdd(this IMemoryCache memoryCache, string key, object? val, TimeSpan? expiration = null)
        {
            memoryCache.TryRemove(key);
            
            var options = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            
            // If cache has a size limit configured, ensure each entry has a Size
            // Setting Size is harmless when no size limit is configured.
            options.Size = 1;
            
            // Add eviction callback for statistics
            options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                if (evictedKey is string keyStr)
                {
                    _keyAccessTimes.TryRemove(keyStr, out _);
                    IncrementCounter(ref _totalRemovals);
                    
                    // Update cache statistics
                    var stats = _cacheStats.GetOrAdd(GetCacheCategory(keyStr), _ => new CacheStatistics());
                    stats.IncrementRemovals();
                }
            });
            
            memoryCache.Set(key, val, options);
            _keyAccessTimes[key] = DateTime.UtcNow;
            IncrementCounter(ref _totalSets);
            
            // Update category statistics
            var categoryStats = _cacheStats.GetOrAdd(GetCacheCategory(key), _ => new CacheStatistics());
            categoryStats.IncrementSets();
        }

        /// <summary>
        /// Enhanced Get with hit/miss tracking. Uses TryGetValue to correctly handle value types.
        /// </summary>
        public static T? GetWithStats<T>(this IMemoryCache memoryCache, string key)
        {
            if (memoryCache.TryGetValue(key, out var obj) && obj is T found)
            {
                IncrementCounter(ref _totalHits);
                _keyAccessTimes[key] = DateTime.UtcNow;
                var stats = _cacheStats.GetOrAdd(GetCacheCategory(key), _ => new CacheStatistics());
                stats.IncrementHits();
                return found;
            }
            else
            {
                IncrementCounter(ref _totalMisses);
                var stats = _cacheStats.GetOrAdd(GetCacheCategory(key), _ => new CacheStatistics());
                stats.IncrementMisses();
                return default;
            }
        }

        public static void TryRemove(this IMemoryCache memoryCache, string key)
        {
            if (memoryCache.Get(key) is not null) 
            {
                memoryCache.Remove(key);
                _keyAccessTimes.TryRemove(key, out _);
                IncrementCounter(ref _totalRemovals);
            }
        }

        private static volatile IMemoryCache? _memoryCache;
        private static readonly object _lockObject = new();

        public static IMemoryCache SharedMemoryCache
        {
            get
            {
                if (_memoryCache == null)
                    lock (_lockObject)
                    {
                        if (_memoryCache == null)
                        {
                            var options = new MemoryCacheOptions
                            {
                                SizeLimit = GetCacheSizeLimit(),
                                CompactionPercentage = 0.25, // Remove 25% when limit reached
                                ExpirationScanFrequency = TimeSpan.FromMinutes(2)
                            };
                            _memoryCache = new MemoryCache(options);
                        }
                    }
                return _memoryCache;
            }
            set
            {
                lock (_lockObject)
                {
                    var oldCache = _memoryCache;
                    _memoryCache = value;
                    if (oldCache is IDisposable disposable) disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// Dispose the shared cache when application shuts down
        /// </summary>
        public static void DisposeSharedCache()
        {
            lock (_lockObject)
            {
                _cleanupTimer?.Dispose();
                if (_memoryCache is IDisposable disposable) disposable.Dispose();
                _memoryCache = null;
                _cacheStats.Clear();
                _keyAccessTimes.Clear();
            }
        }

        /// <summary>
        /// Get comprehensive cache statistics
        /// </summary>
        public static CacheMetrics GetCacheMetrics()
        {
            return new CacheMetrics
            {
                TotalHits = Interlocked.Read(ref _totalHits),
                TotalMisses = Interlocked.Read(ref _totalMisses),
                TotalSets = Interlocked.Read(ref _totalSets),
                TotalRemovals = Interlocked.Read(ref _totalRemovals),
                HitRatio = CalculateHitRatio(),
                CategoryStats = _cacheStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetSnapshot()),
                ActiveKeysCount = _keyAccessTimes.Count,
                MemoryPressure = GC.GetTotalMemory(false)
            };
        }

        /// <summary>
        /// Get statistics for a specific cache category
        /// </summary>
        public static CacheStatistics? GetCategoryStats(string category)
        {
            return _cacheStats.TryGetValue(category, out var stats) ? stats : null;
        }

        /// <summary>
        /// Clear all cache statistics
        /// </summary>
        public static void ClearStats()
        {
            Interlocked.Exchange(ref _totalHits, 0);
            Interlocked.Exchange(ref _totalMisses, 0);
            Interlocked.Exchange(ref _totalSets, 0);
            Interlocked.Exchange(ref _totalRemovals, 0);
            _cacheStats.Clear();
        }

        /// <summary>
        /// Warm up cache with commonly used data
        /// </summary>
        public static async Task WarmUpCacheAsync(IEnumerable<KeyValuePair<string, Func<Task<object>>>> warmupData)
        {
            var tasks = warmupData.Select(async kvp =>
            {
                try
                {
                    var value = await kvp.Value();
                    SharedMemoryCache.TryAdd(kvp.Key, value, TimeSpan.FromHours(1));
                }
                catch (Exception ex)
                {
                    // Log warming failure but don't stop the process
                    System.Diagnostics.Debug.WriteLine($"Cache warmup failed for key {kvp.Key}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        #region Private Methods

        private static void IncrementCounter(ref long counter)
        {
            Interlocked.Increment(ref counter);
        }

        private static double CalculateHitRatio()
        {
            var hits = Interlocked.Read(ref _totalHits);
            var misses = Interlocked.Read(ref _totalMisses);
            var total = hits + misses;
            return total == 0 ? 0.0 : (double)hits / total;
        }

        private static string GetCacheCategory(string key)
        {
            var parts = key.Split("::");
            return parts.Length > 1 ? parts[0] : "General";
        }

        private static long GetCacheSizeLimit()
        {
            // Dynamic cache size based on available memory
            var totalMemory = GC.GetTotalMemory(false);
            var availableMemory = Math.Max(totalMemory, 1024 * 1024 * 100); // At least 100MB
            return Math.Min(availableMemory / 4, 1024 * 1024 * 500); // Max 500MB
        }

        private static void PerformCleanup(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Remove unused keys older than 30 minutes
                var keysToRemove = _keyAccessTimes
                    .Where(kvp => kvp.Value < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .Take(100) // Limit cleanup batch size
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    SharedMemoryCache.TryRemove(key);
                }

                // Force garbage collection if memory pressure is high
                if (GC.GetTotalMemory(false) > 1024 * 1024 * 1024) // 1GB
                {
                    GC.Collect(1, GCCollectionMode.Optimized);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache cleanup failed: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Cache statistics for monitoring performance
    /// </summary>
    public class CacheStatistics
    {
        private long _hits = 0;
        private long _misses = 0;
        private long _sets = 0;
        private long _removals = 0;

        public void IncrementHits() => Interlocked.Increment(ref _hits);
        public void IncrementMisses() => Interlocked.Increment(ref _misses);
        public void IncrementSets() => Interlocked.Increment(ref _sets);
        public void IncrementRemovals() => Interlocked.Increment(ref _removals);

        public CacheStatisticsSnapshot GetSnapshot()
        {
            var hits = Interlocked.Read(ref _hits);
            var misses = Interlocked.Read(ref _misses);
            var total = hits + misses;
            
            return new CacheStatisticsSnapshot
            {
                Hits = hits,
                Misses = misses,
                Sets = Interlocked.Read(ref _sets),
                Removals = Interlocked.Read(ref _removals),
                HitRatio = total == 0 ? 0.0 : (double)hits / total
            };
        }
    }

    /// <summary>
    /// Immutable snapshot of cache statistics
    /// </summary>
    public record CacheStatisticsSnapshot
    {
        public long Hits { get; init; }
        public long Misses { get; init; }
        public long Sets { get; init; }
        public long Removals { get; init; }
        public double HitRatio { get; init; }
    }

    /// <summary>
    /// Comprehensive cache metrics
    /// </summary>
    public record CacheMetrics
    {
        public long TotalHits { get; init; }
        public long TotalMisses { get; init; }
        public long TotalSets { get; init; }
        public long TotalRemovals { get; init; }
        public double HitRatio { get; init; }
        public Dictionary<string, CacheStatisticsSnapshot> CategoryStats { get; init; } = new();
        public int ActiveKeysCount { get; init; }
        public long MemoryPressure { get; init; }

        public override string ToString()
        {
            return $"Cache Metrics: Hits={TotalHits}, Misses={TotalMisses}, HitRatio={HitRatio:P2}, ActiveKeys={ActiveKeysCount}, Memory={MemoryPressure / 1024 / 1024}MB";
        }
    }
}