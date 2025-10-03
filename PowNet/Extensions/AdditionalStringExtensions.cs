using System.Globalization;
using System.Text;

namespace PowNet.Extensions;

/// <summary>
/// Additional string/text utilities.
/// </summary>
public static class AdditionalStringExtensions
{
    /// <summary>
    /// Removes diacritics/accents from the string (useful for search/slug).
    /// </summary>
    public static string RemoveDiacritics(this string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Creates a URL-safe slug from an input string.
    /// </summary>
    public static string ToSlug(this string? text, bool keepNumbers = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var s = text.RemoveDiacritics().ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetter(ch) || (keepNumbers && char.IsDigit(ch)))
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
                sb.Append('-');
        }
        var slug = sb.ToString();
        // collapse dashes
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    /// <summary>
    /// Masks string preserving start/end characters.
    /// </summary>
    public static string Mask(this string? s, int keepStart = 2, int keepEnd = 2, char mask = '*')
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (keepStart + keepEnd >= s.Length) return new string(mask, s.Length);
        var start = s.Substring(0, Math.Max(0, keepStart));
        var end = s.Substring(s.Length - Math.Max(0, keepEnd));
        var middleLen = s.Length - start.Length - end.Length;
        return start + new string(mask, middleLen) + end;
    }

    /// <summary>
    /// Truncates a string to a maximum byte length using the provided encoding.
    /// Respects character boundaries.
    /// </summary>
    public static string TruncateByBytes(this string? s, int maxBytes, Encoding encoding)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (maxBytes <= 0) return string.Empty;
        if (encoding.GetByteCount(s) <= maxBytes) return s;
        var sb = new StringBuilder();
        var bytes = 0;
        foreach (var ch in s!)
        {
            var count = encoding.GetByteCount(new[] { ch });
            if (bytes + count > maxBytes) break;
            sb.Append(ch);
            bytes += count;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Normalizes newlines to \n.
    /// </summary>
    public static string NormalizeNewlines(this string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>
    /// Converts bytes to Base64Url (RFC 7515/7519 compatible).
    /// </summary>
    public static string ToBase64Url(this byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Decodes Base64Url string to bytes.
    /// </summary>
    public static byte[] FromBase64Url(this string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        switch (b64.Length % 4)
        {
            case 2: b64 += "=="; break;
            case 3: b64 += "="; break;
        }
        return Convert.FromBase64String(b64);
    }
}
