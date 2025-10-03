using System.Diagnostics;

namespace PowNet.Extensions;

/// <summary>
/// Lightweight diagnostics helpers.
/// </summary>
public static class AdditionalDiagnosticsExtensions
{
    /// <summary>
    /// Measures the execution time of a synchronous function.
    /// </summary>
    public static T Measure<T>(this Func<T> func, out TimeSpan elapsed)
    {
        var sw = Stopwatch.StartNew();
        try { return func(); }
        finally { sw.Stop(); elapsed = sw.Elapsed; }
    }

    /// <summary>
    /// Measures the execution time of an async function.
    /// </summary>
    public static async Task<T> MeasureAsync<T>(this Func<Task<T>> func, Action<TimeSpan>? onMeasured = null)
    {
        var sw = Stopwatch.StartNew();
        try { return await func(); }
        finally { sw.Stop(); onMeasured?.Invoke(sw.Elapsed); }
    }

    /// <summary>
    /// Returns the exception chain (this + inner exceptions).
    /// </summary>
    public static IEnumerable<Exception> GetExceptionChain(this Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            yield return e;
    }
}
