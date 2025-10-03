using System.Collections.Concurrent;
using System.Text.Json;
using PowNet.Configuration;
using PowNet.Common;
using PowNet.Logging;

namespace PowNet.Extensions
{
    /// <summary>
    /// Advanced caching extensions with multi-level support for PowNet framework
    /// </summary>
    public static class CacheExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("Cache");
        private static readonly ConcurrentDictionary<string, ICacheProvider> _cacheProviders = new();
        private static readonly CacheStatistics _statistics = new();
        private static readonly object _statisticsLock = new();

        #region Cache Registration

        /// <summary>
        /// Register cache provider
        /// </summary>
        public static void RegisterCacheProvider(string name, ICacheProvider provider)
        {
            _cacheProviders.TryAdd(name, provider);
            _logger.LogInformation("Cache provider registered: {ProviderName}", name);
        }

        /// <summary>
        /// Get cache provider by name
        /// </summary>
        public static ICacheProvider GetCacheProvider(string name = "default")
        {
            if (_cacheProviders.TryGetValue(name, out var provider))
                return provider;

            // Create default in-memory provider if not found
            var defaultProvider = new MemoryCacheProvider();
            _cacheProviders.TryAdd(name, defaultProvider);
            return defaultProvider;
        }

        #endregion

        #region Smart Caching

        /// <summary>
        /// Cache function result with automatic key generation
        /// </summary>
        public static async Task<T> CacheAsync<T>(
            this Func<Task<T>> factory,
            string? cacheKey = null,
            TimeSpan? expiration = null,
            string cacheProvider = "default",
            CacheOptions? options = null)
        {
            cacheKey ??= GenerateCacheKey(factory.Method);
            expiration ??= TimeSpan.FromMinutes(PowNetConfiguration.DefaultCacheExpirationMinutes);
            options ??= CacheOptions.Default;

            var cache = GetCacheProvider(cacheProvider);
            
            // Determine presence using metadata to support value types
            var (cachedValue, metadata) = await cache.GetWithMetadataAsync<T>(cacheKey);
            if (metadata != null)
            {
                RecordCacheHit(cacheKey, cacheProvider);
                return cachedValue!;
            }

            // Execute factory and cache result
            try
            {
                var result = await factory();
                
                if (ShouldCache(result, options))
                {
                    await cache.SetAsync(cacheKey, result, expiration.Value, options.Tags);
                }

                RecordCacheMiss(cacheKey, cacheProvider);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Cache factory execution failed for key: {CacheKey}", cacheKey);
                throw;
            }
        }

        /// <summary>
        /// Cache synchronous function result
        /// </summary>
        public static T Cache<T>(
            this Func<T> factory,
            string? cacheKey = null,
            TimeSpan? expiration = null,
            string cacheProvider = "default",
            CacheOptions? options = null)
        {
            cacheKey ??= GenerateCacheKey(factory.Method);
            expiration ??= TimeSpan.FromMinutes(PowNetConfiguration.DefaultCacheExpirationMinutes);
            options ??= CacheOptions.Default;

            var cache = GetCacheProvider(cacheProvider);
            
            // Use metadata-based presence check to support value types
            var tuple = cache.GetWithMetadataAsync<T>(cacheKey).GetAwaiter().GetResult();
            if (tuple.Metadata != null)
            {
                RecordCacheHit(cacheKey, cacheProvider);
                return tuple.Value!;
            }

            // Execute factory and cache result
            try
            {
                var result = factory();
                
                if (ShouldCache(result, options))
                {
                    cache.Set(cacheKey, result, expiration.Value, options.Tags);
                }

                RecordCacheMiss(cacheKey, cacheProvider);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Cache factory execution failed for key: {CacheKey}", cacheKey);
                throw;
            }
        }

        #endregion

        #region Multi-Level Caching

        /// <summary>
        /// Cache with L1 (memory) and L2 (distributed) layers
        /// </summary>
        public static async Task<T> MultiLevelCacheAsync<T>(
            this Func<Task<T>> factory,
            string cacheKey,
            TimeSpan? l1Expiration = null,
            TimeSpan? l2Expiration = null,
            string l1Provider = "memory",
            string l2Provider = "distributed")
        {
            l1Expiration ??= TimeSpan.FromMinutes(5);
            l2Expiration ??= TimeSpan.FromMinutes(30);

            var l1Cache = GetCacheProvider(l1Provider);
            var l2Cache = GetCacheProvider(l2Provider);

            // Try L1 cache first (metadata presence)
            var (l1Value, l1Meta) = await l1Cache.GetWithMetadataAsync<T>(cacheKey);
            if (l1Meta != null)
            {
                RecordCacheHit($"{cacheKey}:L1", l1Provider);
                return l1Value!;
            }

            // Try L2 cache
            var (l2Value, l2Meta) = await l2Cache.GetWithMetadataAsync<T>(cacheKey);
            if (l2Meta != null)
            {
                // Populate L1 cache
                await l1Cache.SetAsync(cacheKey, l2Value!, l1Expiration.Value);
                RecordCacheHit($"{cacheKey}:L2", l2Provider);
                return l2Value!;
            }

            // Execute factory and populate both caches
            var result = await factory();
            
            await Task.WhenAll(
                l1Cache.SetAsync(cacheKey, result, l1Expiration.Value),
                l2Cache.SetAsync(cacheKey, result, l2Expiration.Value)
            );

            RecordCacheMiss(cacheKey, "multi-level");
            return result;
        }

        #endregion

        #region Conditional Caching

        /// <summary>
        /// Cache with conditional logic
        /// </summary>
        public static async Task<T> CacheIfAsync<T>(
            this Func<Task<T>> factory,
            Func<T, bool> shouldCache,
            string cacheKey,
            TimeSpan? expiration = null,
            string cacheProvider = "default")
        {
            var cache = GetCacheProvider(cacheProvider);
            expiration ??= TimeSpan.FromMinutes(PowNetConfiguration.DefaultCacheExpirationMinutes);

            var (cachedValue, metadata) = await cache.GetWithMetadataAsync<T>(cacheKey);
            if (metadata != null)
            {
                RecordCacheHit(cacheKey, cacheProvider);
                return cachedValue!;
            }

            var result = await factory();
            
            if (shouldCache(result))
            {
                await cache.SetAsync(cacheKey, result, expiration.Value);
            }

            RecordCacheMiss(cacheKey, cacheProvider);
            return result;
        }

        /// <summary>
        /// Refresh cache in background
        /// </summary>
        public static async Task<T> RefreshBehindAsync<T>(
            this Func<Task<T>> factory,
            string cacheKey,
            TimeSpan expiration,
            TimeSpan refreshThreshold,
            string cacheProvider = "default")
        {
            var cache = GetCacheProvider(cacheProvider);
            
            var (value, metadata) = await cache.GetWithMetadataAsync<T>(cacheKey);
            
            if (metadata != null)
            {
                // Check if refresh is needed
                if (metadata.CreatedAt.Add(refreshThreshold) < DateTime.UtcNow)
                {
                    // Refresh in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var newValue = await factory();
                            await cache.SetAsync(cacheKey, newValue, expiration);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogException(ex, "Background cache refresh failed for key: {CacheKey}", cacheKey);
                        }
                    });
                }

                RecordCacheHit(cacheKey, cacheProvider);
                return value!;
            }

            // Cache miss - execute factory
            var result = await factory();
            await cache.SetAsync(cacheKey, result, expiration);
            
            RecordCacheMiss(cacheKey, cacheProvider);
            return result;
        }

        #endregion

        #region Cache Tags & Invalidation

        /// <summary>
        /// Invalidate cache by tags
        /// </summary>
        public static async Task InvalidateByTagsAsync(params string[] tags)
        {
            var tasks = _cacheProviders.Values.Select(provider => provider.InvalidateByTagsAsync(tags));
            await Task.WhenAll(tasks);
            
            _logger.LogInformation("Cache invalidated by tags: {Tags}", string.Join(", ", tags));
        }

        /// <summary>
        /// Invalidate cache by pattern
        /// </summary>
        public static async Task InvalidateByPatternAsync(string pattern, string cacheProvider = "default")
        {
            var cache = GetCacheProvider(cacheProvider);
            await cache.InvalidateByPatternAsync(pattern);
            
            _logger.LogInformation("Cache invalidated by pattern: {Pattern}", pattern);
        }

        #endregion

        #region Cache Warming

        /// <summary>
        /// Warm cache with data
        /// </summary>
        public static async Task WarmCacheAsync<T>(
            string cacheKey,
            Func<Task<T>> factory,
            TimeSpan expiration,
            string cacheProvider = "default")
        {
            var cache = GetCacheProvider(cacheProvider);
            
            try
            {
                var value = await factory();
                await cache.SetAsync(cacheKey, value, expiration);
                
                _logger.LogDebug("Cache warmed for key: {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Cache warming failed for key: {CacheKey}", cacheKey);
            }
        }

        /// <summary>
        /// Warm multiple cache entries
        /// </summary>
        public static async Task WarmCacheBatchAsync<T>(
            Dictionary<string, Func<Task<T>>> factories,
            TimeSpan expiration,
            string cacheProvider = "default",
            int maxConcurrency = 4)
        {
            var cache = GetCacheProvider(cacheProvider);
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = factories.Select(async kvp =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await WarmCacheAsync(kvp.Key, kvp.Value, expiration, cacheProvider);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Cache Statistics

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public static CacheStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                return new CacheStatistics
                {
                    TotalHits = _statistics.TotalHits,
                    TotalMisses = _statistics.TotalMisses,
                    ProviderStats = new Dictionary<string, CacheProviderStats>(_statistics.ProviderStats)
                };
            }
        }

        /// <summary>
        /// Reset cache statistics
        /// </summary>
        public static void ResetStatistics()
        {
            lock (_statisticsLock)
            {
                _statistics.Reset();
            }
        }

        #endregion

        #region Private Helper Methods

        private static string GenerateCacheKey(System.Reflection.MethodInfo method)
        {
            return $"{method.DeclaringType?.FullName}.{method.Name}";
        }

        private static bool ShouldCache<T>(T result, CacheOptions options)
        {
            if (result == null && !options.CacheNullValues)
                return false;

            if (result is string str && string.IsNullOrEmpty(str) && !options.CacheEmptyStrings)
                return false;

            if (result is System.Collections.ICollection collection && collection.Count == 0 && !options.CacheEmptyCollections)
                return false;

            return true;
        }

        private static void RecordCacheHit(string cacheKey, string provider)
        {
            lock (_statisticsLock)
            {
                _statistics.RecordHit(provider);
            }
        }

        private static void RecordCacheMiss(string cacheKey, string provider)
        {
            lock (_statisticsLock)
            {
                _statistics.RecordMiss(provider);
            }
        }

        #endregion
    }

    #region Cache Interfaces

    public interface ICacheProvider
    {
        T? Get<T>(string key);
        Task<T?> GetAsync<T>(string key);
        Task<(T? Value, CacheMetadata? Metadata)> GetWithMetadataAsync<T>(string key);
        
        void Set<T>(string key, T value, TimeSpan expiration, params string[] tags);
        Task SetAsync<T>(string key, T value, TimeSpan expiration, params string[] tags);
        
        bool Remove(string key);
        Task<bool> RemoveAsync(string key);
        
        Task InvalidateByTagsAsync(params string[] tags);
        Task InvalidateByPatternAsync(string pattern);
        
        void Clear();
        Task ClearAsync();
    }

    #endregion

    #region Cache Implementations

    public class MemoryCacheProvider : ICacheProvider
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly Timer _cleanupTimer;

        public MemoryCacheProvider()
        {
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public T? Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                entry.LastAccessed = DateTime.UtcNow;
                return JsonSerializer.Deserialize<T>(entry.Data);
            }

            _cache.TryRemove(key, out _);
            return default;
        }

        public async Task<T? > GetAsync<T>(string key)
        {
            return await Task.FromResult(Get<T>(key));
        }

        public async Task<(T? Value, CacheMetadata? Metadata)> GetWithMetadataAsync<T>(string key)
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                entry.LastAccessed = DateTime.UtcNow;
                var value = JsonSerializer.Deserialize<T>(entry.Data);
                var metadata = new CacheMetadata
                {
                    CreatedAt = entry.CreatedAt,
                    ExpiresAt = entry.ExpiresAt,
                    LastAccessed = entry.LastAccessed,
                    Tags = entry.Tags
                };
                return (value, metadata);
            }

            _cache.TryRemove(key, out _);
            return (default, null);
        }

        public void Set<T>(string key, T value, TimeSpan expiration, params string[] tags)
        {
            var data = JsonSerializer.Serialize(value);
            var entry = new CacheEntry
            {
                Data = data,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiration),
                LastAccessed = DateTime.UtcNow,
                Tags = tags.ToHashSet()
            };

            _cache.AddOrUpdate(key, entry, (k, existing) => entry);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration, params string[] tags)
        {
            await Task.Run(() => Set(key, value, expiration, tags));
        }

        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out _);
        }

        public async Task<bool> RemoveAsync(string key)
        {
            return await Task.FromResult(Remove(key));
        }

        public async Task InvalidateByTagsAsync(params string[] tags)
        {
            var keysToRemove = _cache
                .Where(kvp => tags.Any(tag => kvp.Value.Tags.Contains(tag)))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            await Task.CompletedTask;
        }

        public async Task InvalidateByPatternAsync(string pattern)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern.Replace("*", ".*"));
            var keysToRemove = _cache.Keys
                .Where(key => regex.IsMatch(key))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            await Task.CompletedTask;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public async Task ClearAsync()
        {
            await Task.Run(Clear);
        }

        private void CleanupExpiredItems(object? state)
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    #endregion

    #region Cache Models

    public class CacheEntry
    {
        public string Data { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public HashSet<string> Tags { get; set; } = new();

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public class CacheMetadata
    {
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();
    }

    public class CacheOptions
    {
        public bool CacheNullValues { get; set; } = false;
        public bool CacheEmptyStrings { get; set; } = false;
        public bool CacheEmptyCollections { get; set; } = false;
        public string[] Tags { get; set; } = Array.Empty<string>();

        public static CacheOptions Default => new();
    }

    public class CacheStatistics
    {
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public double HitRate => TotalHits + TotalMisses == 0 ? 0 : (double)TotalHits / (TotalHits + TotalMisses) * 100;
        public Dictionary<string, CacheProviderStats> ProviderStats { get; set; } = new();

        public void RecordHit(string provider)
        {
            TotalHits++;
            if (!ProviderStats.ContainsKey(provider))
                ProviderStats[provider] = new CacheProviderStats();
            ProviderStats[provider].Hits++;
        }

        public void RecordMiss(string provider)
        {
            TotalMisses++;
            if (!ProviderStats.ContainsKey(provider))
                ProviderStats[provider] = new CacheProviderStats();
            ProviderStats[provider].Misses++;
        }

        public void Reset()
        {
            TotalHits = 0;
            TotalMisses = 0;
            ProviderStats.Clear();
        }
    }

    public class CacheProviderStats
    {
        public long Hits { get; set; }
        public long Misses { get; set; }
        public double HitRate => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses) * 100;
    }

    #endregion
}