using Microsoft.Extensions.DependencyInjection;
using PowNet.Abstractions.Api;
using PowNet.Abstractions.Authentication;
using PowNet.Abstractions.Logging;
using PowNet.Abstractions.Telemetry;
using PowNet.Abstractions.Utilities;
using PowNet.Implementations.Api;
using PowNet.Implementations.Authentication;
using PowNet.Implementations.Logging;
using PowNet.Implementations.Telemetry;
using PowNet.Implementations.Utilities;

namespace PowNet.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPowNetCore(this IServiceCollection services)
    {
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IGuard, Guard>();
        services.AddSingleton<ILoggingConfigProvider, LoggingConfigProvider>();
        services.AddSingleton<ILoggingLevelController, LoggingConfigProvider>();
        services.AddSingleton<ITelemetrySpanFactory, TelemetrySpanFactory>();
        return services;
    }

    public static IServiceCollection AddPowNetAuth(this IServiceCollection services)
    {
        services.AddSingleton<IUserCache, InMemoryUserCache>();
        services.AddSingleton<IUserIdentityFactory, DefaultUserIdentityFactory>();
        services.AddSingleton<ITokenUserResolver, TokenUserResolver>();
        return services;
    }

    public static IServiceCollection AddPowNetApi(this IServiceCollection services)
    {
        services.AddSingleton<IApiAuthorizationService, ApiAuthorizationService>();
        services.AddSingleton<IApiCacheService, ApiCacheService>();
        services.AddSingleton<IActivityLogger, ActivityLogger>();
        return services;
    }

    public static IServiceCollection AddPowNetAll(this IServiceCollection services)
        => services.AddPowNetCore().AddPowNetAuth().AddPowNetApi();
}
