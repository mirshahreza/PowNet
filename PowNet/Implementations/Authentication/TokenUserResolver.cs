using System.Text.Json.Nodes;
using PowNet.Abstractions.Authentication;
using PowNet.Configuration;
using PowNet.Extensions; // for ToStringEmpty + DecryptAesGcm + ToJsonObjectByBuiltIn
using PowNet.Models;

namespace PowNet.Implementations.Authentication;

public sealed class TokenUserResolver : ITokenUserResolver
{
    private readonly IUserIdentityFactory _factory;
    private readonly IUserCache _cache;

    public TokenUserResolver(IUserIdentityFactory factory, IUserCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    public IUserIdentity ResolveFromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return UserServerObject.NobodyUserServerObject;
        try
        {
            var tokenValue = token.ToStringEmpty();
            if (tokenValue.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase)) tokenValue = tokenValue.Substring(7);
            // Original code used tokenValue.Decode(secret) which maps to AES decrypt of the serialized JSON
            var jsonPlain = tokenValue.DecryptAesGcm(PowNetConfiguration.EncryptionSecret);
            var json = jsonPlain.ToJsonObjectByBuiltIn();
            string userName = json["UserName"].ToStringEmpty();
            if (string.IsNullOrEmpty(userName)) return UserServerObject.NobodyUserServerObject;
            if (_cache.TryGet(userName, out var cached)) return cached;
            var created = _factory.Create(userName);
            _cache.Set(created, TimeSpan.FromMinutes(30));
            return created;
        }
        catch
        {
            return UserServerObject.NobodyUserServerObject;
        }
    }

    public IUserIdentity ResolveFromHttpContext(Microsoft.AspNetCore.Http.HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("token", out var token)) return UserServerObject.NobodyUserServerObject;
        return ResolveFromToken(token.ToString());
    }
}
