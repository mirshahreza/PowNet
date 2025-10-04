using Microsoft.Extensions.Caching.Memory;
using PowNet.Abstractions.Api;
using PowNet.Abstractions.Authentication;
using PowNet.Configuration;
using PowNet.Services;

namespace PowNet.Implementations.Api;

public sealed class ApiCacheService : IApiCacheService
{
    private readonly IMemoryCache _cache;
    public ApiCacheService() : this(MemoryService.SharedMemoryCache) { }
    public ApiCacheService(IMemoryCache cache) => _cache = cache;

    public string BuildKey(IApiCallInfo call, IUserIdentity user, IApiConfiguration apiConfiguration)
    {
        var perUser = apiConfiguration.CachingEnabled && (apiConfiguration is ApiConfiguration conf && conf.CacheLevel == PowNet.Common.CacheLevel.PerUser);
        return $"Response::{call.Controller}_{call.Action}{(perUser ? "_" + user.UserName : string.Empty)}";
    }

    public bool TryGet(string key, out CachedApiResponse? response)
    {
        if (_cache.TryGetValue(key, out CachedApiResponse? cached))
        {
            response = cached;
            return true;
        }
        response = null;
        return false;
    }

    public void Set(string key, CachedApiResponse response, TimeSpan ttl)
    {
        _cache.Set(key, response, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
    }
}
