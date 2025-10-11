namespace PowNet.Abstractions.Utilities
{
    public interface ISystemClock
    {
        DateTime UtcNow { get; }
    }

    public interface IGuard
    {
        T NotNull<T>(T value, string paramName) where T : class;
        string NotNullOrEmpty(string? value, string paramName);
    }
}
