using System.Buffers;
using System.Security.Cryptography;

namespace PowNet.Extensions;

/// <summary>
/// Extra security/validation utilities.
/// </summary>
public static class AdditionalSecurityExtensions
{
    /// <summary>
    /// Constant-time equality comparison to avoid timing attacks.
    /// </summary>
    public static bool ConstantTimeEquals(this ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    /// <summary>
    /// Join path segments safely under a root directory to prevent path traversal.
    /// </summary>
    public static string SafeJoin(this DirectoryInfo root, params string[] segments)
    {
        var combined = segments.Aggregate(root.FullName, Path.Combine);
        var full = Path.GetFullPath(combined);
        if (!full.StartsWith(root.FullName, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal detected.");
        return full;
    }

    /// <summary>
    /// Ensures the value is not default; throws ArgumentException otherwise.
    /// </summary>
    public static T EnsureNotDefault<T>(this T value, string paramName)
    {
        if (EqualityComparer<T>.Default.Equals(value, default!))
            throw new ArgumentException($"Parameter '{paramName}' must not be default.", paramName);
        return value;
    }
}
