using Microsoft.AspNetCore.Http;
using PowNet.Abstractions.Authentication;

namespace PowNet.Abstractions.Api
{
    /// <summary>
    /// Minimal read-only representation of an API call descriptor (decoupled from concrete PowNet models for phase 1).
    /// </summary>
    public interface IApiCallInfo
    {
        string? Namespace { get; }
        string? Controller { get; }
        string? Action { get; }
        string RequestPath { get; }
        IReadOnlyDictionary<string,string?> RouteValues { get; }
    }

    /// <summary>
    /// Parser that extracts an API call description from the current HttpContext.
    /// </summary>
    public interface IApiCallParser
    {
        IApiCallInfo Parse(HttpContext httpContext);
    }

    /// <summary>
    /// Abstract API configuration contract (subset) needed by gateway / auth.
    /// </summary>
    public interface IApiConfiguration
    {
        string ApiName { get; }
        bool CachingEnabled { get; }
        bool LoggingEnabled { get; }
        TimeSpan? AbsoluteCacheDuration { get; }
    }

    /// <summary>
    /// Authorization service responsible to check if a user can call an API.
    /// </summary>
    public interface IApiAuthorizationService
    {
        bool HasAccess(IUserIdentity user, IApiConfiguration apiConfiguration);
    }

    /// <summary>
    /// API level cache handling.
    /// </summary>
    public interface IApiCacheService
    {
        string BuildKey(IApiCallInfo call, IUserIdentity user, IApiConfiguration apiConfiguration);
        bool TryGet(string key, out CachedApiResponse? response);
        void Set(string key, CachedApiResponse response, TimeSpan ttl);
    }

    /// <summary>
    /// Structured activity logger abstraction (gateway -> logging provider).
    /// </summary>
    public interface IActivityLogger
    {
        void LogActivity(HttpContext context, IUserIdentity user, IApiCallInfo call, string rowId, bool success, string message, int durationMs);
        void LogError(string message, Exception? ex = null);
        void LogDebug(string message);
    }

    public sealed record CachedApiResponse(string Content, string? ContentType);
}

namespace PowNet.Abstractions.Authentication
{
    /// <summary>
    /// Minimal user identity abstraction (gateway / auth / cache rely on only these fields).
    /// </summary>
    public interface IUserIdentity
    {
        int Id { get; }
        string UserName { get; }
        bool IsAnonymous { get; }
        bool IsInRole(string roleName);
    }
}
