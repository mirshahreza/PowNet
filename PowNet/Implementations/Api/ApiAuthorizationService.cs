using PowNet.Abstractions.Api;
using PowNet.Abstractions.Authentication;
using PowNet.Configuration;
using PowNet.Models;

namespace PowNet.Implementations.Api;

public sealed class ApiAuthorizationService : IApiAuthorizationService
{
    public bool HasAccess(IUserIdentity user, IApiConfiguration apiConfiguration)
    {
        if (user is UserServerObject uso && apiConfiguration is ApiConfiguration concrete)
        {
            return uso.HasAccess(concrete);
        }
        // If we only have abstraction, allow basic open rules if exposed
        return user.IsAnonymous == false; // fallback permissive for authenticated users
    }
}
