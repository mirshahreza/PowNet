using Microsoft.Extensions.Caching.Memory;
using PowNet.Common;

namespace PowNet.Configuration
{
    public record ApiConfiguration
    {
        public string ApiName { get; set; } = "";
        public CheckAccessLevel CheckAccessLevel { get; set; } = CheckAccessLevel.CheckAccessRules;
        public List<int>? AllowedRoles { get; set; }
        public List<int>? AllowedUsers { get; set; }
        public List<int>? DeniedRoles { get; set; }
        public List<int>? DeniedUsers { get; set; }
        public CacheLevel CacheLevel { get; set; } = CacheLevel.None;
        public int CacheSeconds { get; set; } = 0;
        public bool LogEnabled { get; set; } = false;
    }

    public static class ApiConfigurationExtensions
    {
        public static bool IsCachingEnabled(this ApiConfiguration apiConf)
        {
            return (apiConf.CacheLevel == CacheLevel.AllUsers || apiConf.CacheLevel == CacheLevel.PerUser) && apiConf.CacheSeconds > 0;
        }

        public static bool IsLoggingEnabled(this ApiConfiguration apiConf)
        {
            return apiConf.LogEnabled;
        }

        public static MemoryCacheEntryOptions GetCacheOptions(this ApiConfiguration apiConf)
        {
            return new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(apiConf.CacheSeconds) };
        }
    }
}