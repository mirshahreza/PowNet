using PowNet.Abstractions.Utilities;

namespace PowNet.Implementations.Utilities;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class Guard : IGuard
{
    public T NotNull<T>(T value, string paramName) where T : class
    {
        if (value is null) throw new ArgumentNullException(paramName);
        return value;
    }

    public string NotNullOrEmpty(string? value, string paramName)
    {
        if (string.IsNullOrEmpty(value)) throw new ArgumentException("Value cannot be null or empty", paramName);
        return value;
    }
}
