using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Concurrent;
using PowNet.Configuration;
using PowNet.Common;

namespace PowNet.Extensions
{
    /// <summary>
    /// High-performance string extensions with caching and optimization
    /// </summary>
    public static partial class StringExtensions
    {
        // StringBuilder pool for better memory management
        private static readonly ConcurrentQueue<StringBuilder> _stringBuilderPool = new();
        private static readonly object _poolLock = new();
        private static readonly int MaxPoolSize = Environment.ProcessorCount * 2;
        private const int DefaultStringBuilderCapacity = 256;

        // String interning cache for frequently used strings
        private static readonly ConcurrentDictionary<string, string> _internedStrings = new();
        private const int MaxInternedStrings = 10000;

        // Pre-compiled regex patterns for better performance
        private static readonly Lazy<Regex> _whitelinesRegexCompiled = new(() => 
            new Regex(@"^\s+$[\r\n]*", RegexOptions.Multiline | RegexOptions.Compiled));
        
        private static readonly Lazy<Regex> _sqlParamsRegexCompiled = new(() => 
            new Regex(@"(\?|\@\w+)", RegexOptions.Multiline | RegexOptions.Compiled));

        // SQL injection patterns - compiled for performance
        private static readonly Lazy<Regex> _sqlInjectionRegex1 = new(() =>
            new Regex(@"'\s*or\s*\d=\d\s*--", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        
        private static readonly Lazy<Regex> _sqlInjectionRegex2 = new(() =>
            new Regex(@"'\s*or\s*true\s*--", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        
        private static readonly Lazy<Regex> _sqlInjectionRegex3 = new(() =>
            new Regex(@"'\s*union\s*select", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        
        private static readonly Lazy<Regex> _sqlInjectionRegex4 = new(() =>
            new Regex(@"drop\s+table", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        
        private static readonly Lazy<Regex> _sqlInjectionRegex5 = new(() =>
            new Regex(@"alter\s+table", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        
        private static readonly Lazy<Regex> _sqlInjectionRegex6 = new(() =>
            new Regex(@"xp_cmdshell", RegexOptions.IgnoreCase | RegexOptions.Compiled));

        #region StringBuilder Pool Management

        /// <summary>
        /// Get a StringBuilder from the pool or create a new one
        /// </summary>
        private static StringBuilder GetPooledStringBuilder(int capacity = DefaultStringBuilderCapacity)
        {
            if (_stringBuilderPool.TryDequeue(out var sb))
            {
                sb.Clear();
                if (sb.Capacity < capacity)
                {
                    sb.Capacity = capacity;
                }
                return sb;
            }
            return new StringBuilder(capacity);
        }

        /// <summary>
        /// Return a StringBuilder to the pool
        /// </summary>
        private static void ReturnStringBuilder(StringBuilder sb)
        {
            if (sb.Capacity <= 4096 && _stringBuilderPool.Count < MaxPoolSize)
            {
                _stringBuilderPool.Enqueue(sb);
            }
        }

        #endregion

        #region String Interning

        /// <summary>
        /// Intern a string for memory efficiency with frequently used strings
        /// </summary>
        public static string InternString(this string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            
            if (_internedStrings.Count >= MaxInternedStrings)
            {
                // Clear half of the cache when it gets too large
                var keysToRemove = _internedStrings.Keys.Take(MaxInternedStrings / 2).ToArray();
                foreach (var key in keysToRemove)
                {
                    _internedStrings.TryRemove(key, out _);
                }
            }
            
            return _internedStrings.GetOrAdd(s, string.Intern);
        }

        #endregion

        #region Enhanced String Operations

        public static string GetUniqueName(string prefix = "param")
        {
            var sb = GetPooledStringBuilder(prefix.Length + 32);
            try
            {
                sb.Append(prefix);
                sb.Append(Guid.NewGuid().ToString("N")); // "N" format is faster
                return sb.ToString();
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }

        public static string GetRandomName(string prefix = "param")
        {
            var sb = GetPooledStringBuilder(prefix.Length + 10);
            try
            {
                sb.Append(prefix);
                sb.Append(Random.Shared.Next(100)); // Use Random.Shared for .NET 6+
                return sb.ToString();
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }

        public static string TransToX2(this string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            
            var sb = GetPooledStringBuilder(s.Length * 2);
            try
            {
                sb.Append(s);
                sb.Append(s);
                return sb.ToString();
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }

        public static byte[] ToByteArray(this string fileString)
        {
            return Convert.FromBase64String(fileString);
        }

        /// <summary>
        /// High-performance string truncation using Span
        /// </summary>
        public static string TruncateTo(this string value, int maxLength)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length <= maxLength) return value;
            
            return value.AsSpan(0, maxLength).ToString();
        }

        /// <summary>
        /// Optimized path normalization
        /// </summary>
        public static string NormalizeAsHostPath(this string path, bool removeBasePath = true)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            
            var sb = GetPooledStringBuilder(path.Length);
            try
            {
                var span = path.AsSpan();
                
                // Remove leading slash
                if (span.StartsWith("/"))
                {
                    span = span[1..];
                }
                
                sb.Append(span);
                
                // Replace backslashes with forward slashes
                sb.Replace('\\', '/');
                
                // Remove double slashes
                var result = sb.ToString();
                while (result.Contains("//"))
                {
                    result = result.Replace("//", "/");
                }
                
                if (removeBasePath)
                {
                    var projectRoot = PowNetConfiguration.ProjectRoot.FullName.NormalizeAsHostPath(false);
                    if (result.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        result = result[projectRoot.Length..];
                      }
                }
                
                // Remove leading slash again
                if (result.StartsWith("/"))
                {
                    result = result[1..];
                }
                
                return result;
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }

        public static Tuple<int, int> ToRangeMinValue(this string? s)
        {
            if (s is null || s.Trim() == "") return new Tuple<int, int>(1, 100);
            
            var openParenIndex = s.IndexOf('(');
            if (openParenIndex == -1) return new Tuple<int, int>(1, 100);
            
            var closeParenIndex = s.IndexOf(')', openParenIndex);
            if (closeParenIndex == -1) return new Tuple<int, int>(1, 100);
            
            var content = s.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1);
            var commaIndex = content.IndexOf(',');
            
            if (commaIndex == -1)
            {
                var min = content.ToIntSafe(1);
                return new Tuple<int, int>(min, 100);
            }
            
            var minPart = content.Substring(0, commaIndex);
            var maxPart = content.Substring(commaIndex + 1);
            
            return new Tuple<int, int>(minPart.ToIntSafe(1), maxPart.ToIntSafe(100));
        }

        /// <summary>
        /// High-performance case-insensitive StartsWith using Span
        /// </summary>
        public static bool StartsWithIgnoreCase(this string? s, string? testString)
        {
            if (s is null || testString is null) return false;
            if (s.Length == 0 || testString.Length == 0) return false;
            if (s.Length < testString.Length) return false;
            
            return s.AsSpan().StartsWith(testString.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// High-performance case-insensitive EndsWith using Span
        /// </summary>
        public static bool EndsWithIgnoreCase(this string? s, string? testString)
        {
            if (s is null || testString is null) return false;
            if (s.Length == 0 || testString.Length == 0) return false;
            if (s.Length < testString.Length) return false;
            
            return s.AsSpan().EndsWith(testString.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool EndsWithIgnoreCase(this string? s, List<string> testStringList)
        {
            if (s is null || s.Length == 0 || testStringList is null || testStringList.Count == 0) return false;
            
            var span = s.AsSpan();
            foreach (var item in testStringList)
            {
                if (!string.IsNullOrEmpty(item) && span.EndsWith(item.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// High-performance case-insensitive Equals using String.Equals
        /// </summary>
        public static bool EqualsIgnoreCase(this string? s, string? testString)
        {
            if (s is null || testString is null) return false;
            return string.Equals(s, testString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// High-performance case-insensitive Contains
        /// </summary>
        public static bool ContainsIgnoreCase(this string? s, string? testString)
        {
            if (s is null || testString is null) return false;
            if (s.Length == 0 || testString.Length == 0) return false;
            
            return s.Contains(testString, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsIgnoreCase(this string? s, List<string> testStringList)
        {
            if (s is null || s.Length == 0 || testStringList is null || testStringList.Count == 0) return false;
            
            foreach (var item in testStringList)
            {
                if (!string.IsNullOrEmpty(item) && s.ContainsIgnoreCase(item))
                    return true;
            }
            return false;
        }

        #endregion

        #region String Manipulation

        public static string ReplaceSafe(this string? s, string? v1, string v2)
        {
            if (s is null || s.Length == 0) return string.Empty;
            if (v1 is null || v1.Length == 0) return s;
            return s.Replace(v1, v2);
        }

        /// <summary>
        /// Optimized common prefix calculation using Span
        /// </summary>
        public static string BeginningCommonPart(this string? s1, string s2)
        {
            if (s1 is null || s2 is null) return string.Empty;
            
            var span1 = s1.AsSpan();
            var span2 = s2.AsSpan();
            var minLen = Math.Min(span1.Length, span2.Length);
            
            var commonLength = 0;
            for (var i = 0; i < minLen; i++)
            {
                if (span1[i] == span2[i])
                    commonLength++;
                else
                    break;
            }
            
            return commonLength == 0 ? string.Empty : s1[..commonLength];
        }

        public static string FixNull(this string? s, string alternate)
        {
            return s ?? alternate;
        }

        public static bool IsNullOrEmpty(this string? s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        public static string FixNullOrEmpty(this string? s, string alternate)
        {
            return string.IsNullOrWhiteSpace(s) ? alternate : s;
        }

        /// <summary>
        /// Optimized string repetition using StringBuilder
        /// </summary>
        public static string RepeatN(this string s, int n)
        {
            if (n <= 0) return string.Empty;
            if (n == 1) return s;
            
            var sb = GetPooledStringBuilder(s.Length * n);
            try
            {
                for (int i = 0; i < n; i++)
                {
                    sb.Append(s);
                }
                return sb.ToString();
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }

        /// <summary>
        /// High-performance last occurrence replacement
        /// </summary>
        public static string ReplaceLastOccurrence(this string s, string find, string replace)
        {
            int place = s.LastIndexOf(find, StringComparison.Ordinal);
            if (place == -1) return s;
            
            var sb = GetPooledStringBuilder(s.Length + replace.Length - find.Length);
            try
            {
                sb.Append(s, 0, place);
                sb.Append(replace);
                sb.Append(s, place + find.Length, s.Length - place - find.Length);
                return sb.ToString();
            }
            finally
            {
                ReturnStringBuilder(sb);
            }
        }

        /// <summary>
        /// Optimized empty line removal using compiled regex
        /// </summary>
        public static string RemoveUnnecessaryEmptyLines(this string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            
            var result = s;
            var newLine = Environment.NewLine;
            
            // Use optimized approach for common patterns
            var fourNewLines = newLine + newLine + newLine + newLine;
            var threeNewLines = newLine + newLine + newLine;
            var twoNewLines = newLine + newLine;
            
            while (result.Contains(fourNewLines))
                result = result.Replace(fourNewLines, newLine);
            while (result.Contains(threeNewLines))
                result = result.Replace(threeNewLines, newLine);
            while (result.Contains(twoNewLines))
                result = result.Replace(twoNewLines, newLine);
                
            return result;
        }

        #endregion

        #region Validation Methods

        public static void ValidateStringNotNullOrEmpty(this string s, string paramName)
        {
            if (s.IsNullOrEmpty())
            {
                throw new PowNetValidationException($"Parameter '{paramName}' cannot be null or empty", paramName, s);
            }
        }

        public static void ValidateStringIsNotPotentialSqlInjection(this string s, string paramName)
        {
            if (s.IsPotentialSqlInjection())
            {
                throw new PowNetSecurityException($"Parameter '{paramName}' contains potential SQL injection", "SQL_INJECTION")
                    .AddParam("ParamName", paramName)
                    .AddParam("Value", s?.Length > 100 ? s[..100] + "..." : s);
            }
        }

        /// <summary>
        /// High-performance whiteline removal using compiled regex
        /// </summary>
        public static string RemoveWhitelines(this string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return _whitelinesRegexCompiled.Value.Replace(s, string.Empty);
        }

        /// <summary>
        /// Optimized SQL parameter extraction using compiled regex
        /// </summary>
        public static List<string> ExtractSqlParameters(this string sql)
        {
            if (string.IsNullOrEmpty(sql)) return [];
            
            return _sqlParamsRegexCompiled.Value
                .Matches(sql)
                .Select(m => m.Value.TrimStart('@', '?'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Enhanced SQL injection detection with compiled regex patterns
        /// </summary>
        public static bool IsPotentialSqlInjection(this string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            // Convert to lowercase once for all checks
            var lowerInput = input.ToLowerInvariant();

            // Fast keyword check using HashSet for O(1) lookup
            if (ContainsSqlKeywords(lowerInput)) return true;

            // Compiled regex pattern checks for more sophisticated patterns
            if (_sqlInjectionRegex1.Value.IsMatch(lowerInput) ||
                _sqlInjectionRegex2.Value.IsMatch(lowerInput) ||
                _sqlInjectionRegex3.Value.IsMatch(lowerInput) ||
                _sqlInjectionRegex4.Value.IsMatch(lowerInput) ||
                _sqlInjectionRegex5.Value.IsMatch(lowerInput) ||
                _sqlInjectionRegex6.Value.IsMatch(lowerInput))
            {
                return true;
            }

            // Fast special character check
            return ContainsDangerousChars(input);
        }

        #endregion

        #region Private Helper Methods

        private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "select", "union", "insert", "update", "delete", "drop",
            "alter", "truncate", "exec", "execute", "xp_cmdshell", "sp_executesql",
            "cast", "convert", "varchar", "nvarchar", "char", "nchar",
            "information_schema", "sysobjects", "syscolumns", "master",
            "declare", "waitfor", "delay", "benchmark", "sleep"
        };

        private static readonly HashSet<char> DangerousChars = new() { '\'', '-', ';', '/', '*', '#', '=' };

        private static bool ContainsSqlKeywords(string lowerInput)
        {
            // Use spans for efficient word boundary detection
            var span = lowerInput.AsSpan();
            var wordStart = 0;
            
            for (int i = 0; i <= span.Length; i++)
            {
                if (i == span.Length || !char.IsLetterOrDigit(span[i]))
                {
                    if (i > wordStart)
                    {
                        var word = span.Slice(wordStart, i - wordStart);
                        if (SqlKeywords.Contains(word.ToString()))
                            return true;
                    }
                    wordStart = i + 1;
                }
            }
            return false;
        }

        private static bool ContainsDangerousChars(string input)
        {
            var span = input.AsSpan();
            foreach (var ch in span)
            {
                if (DangerousChars.Contains(ch))
                    return true;
            }
            return false;
        }

        #endregion

        #region Regex Properties (for backward compatibility)

        [GeneratedRegex(@"^\s+$[\r\n]*", RegexOptions.Multiline)]
        public static partial Regex WhitelinesRegex();

        [GeneratedRegex(@"shared.translate\(.*?\)", RegexOptions.Multiline)]
        public static partial Regex JsTranslationRegex();

        [GeneratedRegex(@"(\?|\@\w+)", RegexOptions.Multiline)]
        public static partial Regex SqlParamsRegex();

        #endregion

        #region Constants

        public static string NT => "\t";
        public static string NL => Environment.NewLine;

        #endregion

        #region Extension Method Overloads for Spans

        /// <summary>
        /// Span-based extension for better performance
        /// </summary>
        public static int ToIntSafe(this ReadOnlySpan<char> span, int ifHasProblem = -1)
        {
            return int.TryParse(span, out int result) ? result : ifHasProblem;
        }

        #endregion
    }

    #region Performance Monitoring Extensions

    /// <summary>
    /// Extensions for monitoring string operation performance
    /// </summary>
    public static class StringPerformanceExtensions
    {
        private static long _operationCount = 0;
        private static long _totalProcessingTime = 0;

        public static T MeasurePerformance<T>(Func<T> operation, string operationName = "Unknown")
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return operation();
            }
            finally
            {
                sw.Stop();
                Interlocked.Increment(ref _operationCount);
                Interlocked.Add(ref _totalProcessingTime, sw.ElapsedTicks);
                
                if (_operationCount % 1000 == 0) // Log every 1000 operations
                {
                    var avgTime = new TimeSpan(_totalProcessingTime / _operationCount).TotalMicroseconds;
                    System.Diagnostics.Debug.WriteLine($"String operations: {_operationCount}, Avg time: {avgTime:F2}?s");
                }
            }
        }

        public static (long Count, double AvgTimeMicroseconds) GetPerformanceStats()
        {
            var count = Interlocked.Read(ref _operationCount);
            var totalTime = Interlocked.Read(ref _totalProcessingTime);
            var avgTime = count == 0 ? 0.0 : new TimeSpan(totalTime / count).TotalMicroseconds;
            return (count, avgTime);
        }
    }

    #endregion
}