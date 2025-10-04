using PowNet.Abstractions.Authentication;
using PowNet.Models;

namespace PowNet.Implementations.Authentication;

public sealed class DefaultUserIdentityFactory : IUserIdentityFactory
{
    public IUserIdentity Create(string userName)
    {
        // Placeholder: would load from database or other store
        return new UserServerObject
        {
            Id = Math.Abs(userName.GetHashCode()),
            UserName = userName,
            Roles = new List<Role>()
        };
    }
}
