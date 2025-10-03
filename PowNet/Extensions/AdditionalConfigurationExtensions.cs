using Microsoft.Extensions.Configuration;

namespace PowNet.Extensions;

/// <summary>
/// Configuration helpers without Options dependency.
/// </summary>
public static class AdditionalConfigurationExtensions
{
    /// <summary>
    /// Get required configuration value or throw if missing.
    /// </summary>
    public static T GetRequired<T>(this IConfiguration cfg, string key)
    {
        var val = cfg.GetValue<T>(key);
        if (EqualityComparer<T>.Default.Equals(val, default!))
            throw new InvalidOperationException($"Missing required configuration key: '{key}'.");
        return val!;
    }

    /// <summary>
    /// Bind section or return default instance.
    /// </summary>
    public static T BindOrDefault<T>(this IConfiguration cfg, string section, T @default) where T : notnull
    {
        var obj = @default;
        cfg.GetSection(section).Bind(obj);
        return obj;
    }

    /// <summary>
    /// Observe changes on a key with throttle.
    /// </summary>
    public static IDisposable OnChangeAsync(this IConfiguration cfg, string key, Func<string, Task> handler, TimeSpan throttle)
    {
        var locker = new object();
        DateTime last = DateTime.MinValue;
        string? lastVal = cfg[key];
        var token = cfg.GetReloadToken();
        var reg = token.RegisterChangeCallback(async _ =>
        {
            lock (locker)
            {
                if (DateTime.UtcNow - last < throttle) return;
                last = DateTime.UtcNow;
            }
            var current = cfg[key] ?? string.Empty;
            if (!string.Equals(current, lastVal, StringComparison.Ordinal))
            {
                lastVal = current;
                await handler(current);
            }
        }, null);
        return reg;
    }
}
