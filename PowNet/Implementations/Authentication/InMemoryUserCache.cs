using PowNet.Abstractions.Authentication;
using System.Collections.Concurrent;

namespace PowNet.Implementations.Authentication;

public sealed class InMemoryUserCache : IUserCache
{
    private readonly ConcurrentDictionary<string, (IUserIdentity user, DateTime expires)> _store = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string userName, out IUserIdentity user)
    {
        if (_store.TryGetValue(userName, out var entry))
        {
            if (entry.expires > DateTime.UtcNow)
            {
                user = entry.user;
                return true;
            }
            _store.TryRemove(userName, out _);
        }
        user = default!;
        return false;
    }

    public void Set(IUserIdentity user, TimeSpan ttl)
    {
        _store[user.UserName] = (user, DateTime.UtcNow.Add(ttl));
    }
}
