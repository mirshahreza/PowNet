using Microsoft.AspNetCore.Http;

namespace PowNet.Abstractions.Authentication
{
    /// <summary>
    /// Resolves user identity from token or HttpContext.
    /// </summary>
    public interface ITokenUserResolver
    {
        IUserIdentity ResolveFromToken(string? token);
        IUserIdentity ResolveFromHttpContext(HttpContext context);
    }

    /// <summary>
    /// In-memory user cache abstraction.
    /// </summary>
    public interface IUserCache
    {
        bool TryGet(string userName, out IUserIdentity user);
        void Set(IUserIdentity user, TimeSpan ttl);
    }

    /// <summary>
    /// Factory for creating full user identity from persistent store.
    /// </summary>
    public interface IUserIdentityFactory
    {
        IUserIdentity Create(string userName);
    }
}
