using System.Text.RegularExpressions;
using System.Net;
using PowNet.Common;

namespace PowNet.Extensions
{
    /// <summary>
    /// Comprehensive security and validation extensions for PowNet framework
    /// </summary>
    public static partial class SecurityExtensions
    {
        #region Input Validation

        /// <summary>
        /// Validates if input is safe from SQL injection attacks
        /// </summary>
        public static ValidationResult ValidateSqlSafety(this string? input, string? parameterName = null)
        {
            if (string.IsNullOrEmpty(input))
                return ValidationResult.Success();

            var issues = new List<string>();

            // Check for dangerous SQL keywords
            if (ContainsDangerousSqlKeywords(input))
                issues.Add("Contains potentially dangerous SQL keywords");

            // Check for SQL injection patterns
            if (ContainsSqlInjectionPatterns(input))
                issues.Add("Contains SQL injection patterns");

            // Check for dangerous characters
            if (ContainsDangerousChars(input))
                issues.Add("Contains potentially dangerous characters");

            // Check for encoded attacks
            if (ContainsEncodedAttacks(input))
                issues.Add("Contains encoded attack patterns");

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"SQL safety validation failed for parameter '{parameterName}'", issues);
        }

        /// <summary>
        /// Validates input against XSS (Cross-Site Scripting) attacks
        /// </summary>
        public static ValidationResult ValidateXssSafety(this string? input, string? parameterName = null)
        {
            if (string.IsNullOrEmpty(input))
                return ValidationResult.Success();

            var issues = new List<string>();

            // Check for script tags
            if (ContainsScriptTags(input))
                issues.Add("Contains script tags");

            // Check for event handlers
            if (ContainsEventHandlers(input))
                issues.Add("Contains JavaScript event handlers");

            // Check for javascript: protocol
            if (ContainsJavaScriptProtocol(input))
                issues.Add("Contains javascript: protocol");

            // Check for data: protocol with scripts
            if (ContainsDangerousDataProtocol(input))
                issues.Add("Contains dangerous data: protocol");

            // Check for encoded XSS attempts
            if (ContainsEncodedXss(input))
                issues.Add("Contains encoded XSS patterns");

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"XSS safety validation failed for parameter '{parameterName}'", issues);
        }

        /// <summary>
        /// Validates file path for path traversal attacks
        /// </summary>
        public static ValidationResult ValidatePathSafety(this string? filePath, string? parameterName = null, bool allowAbsolutePaths = false)
        {
            if (string.IsNullOrEmpty(filePath))
                return ValidationResult.Success();

            var issues = new List<string>();

            // Normalize path separators
            var normalizedPath = filePath.Replace('\\', '/');

            // Check for path traversal patterns
            if (normalizedPath.Contains("../") || normalizedPath.Contains("..\\"))
                issues.Add("Contains path traversal patterns");

            // Check for absolute paths if not allowed
            if (!allowAbsolutePaths && Path.IsPathRooted(normalizedPath))
                issues.Add("Absolute paths are not allowed");

            // Check for dangerous file extensions
            if (HasDangerousFileExtension(filePath))
                issues.Add("Contains dangerous file extension");

            // Check for null bytes
            if (filePath.Contains('\0'))
                issues.Add("Contains null bytes");

            // Check for reserved names (Windows)
            if (ContainsReservedNames(Path.GetFileName(filePath)))
                issues.Add("Contains reserved system names");

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"Path safety validation failed for parameter '{parameterName}'", issues);
        }

        /// <summary>
        /// Validates email format with comprehensive checks
        /// </summary>
        public static ValidationResult ValidateEmail(this string? email, string? parameterName = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return ValidationResult.Failure($"Email is required for parameter '{parameterName}'");

            var issues = new List<string>();

            // Basic format validation
            if (!EmailRegex().IsMatch(email))
                issues.Add("Invalid email format");

            // Length validation
            if (email.Length > 254) // RFC 5321 limit
                issues.Add("Email too long (max 254 characters)");

            // Local part validation
            var parts = email.Split('@');
            if (parts.Length == 2)
            {
                var localPart = parts[0];
                var domainPart = parts[1];

                if (localPart.Length > 64) // RFC 5321 limit
                    issues.Add("Local part too long (max 64 characters)");

                if (domainPart.Length > 255) // RFC 1035 limit
                    issues.Add("Domain part too long (max 255 characters)");

                // Check for consecutive dots
                if (email.Contains(".."))
                    issues.Add("Contains consecutive dots");
            }

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"Email validation failed for parameter '{parameterName}'", issues);
        }

        /// <summary>
        /// Validates phone number format
        /// </summary>
        public static ValidationResult ValidatePhoneNumber(this string? phoneNumber, string? parameterName = null, bool requireCountryCode = false)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return ValidationResult.Failure($"Phone number is required for parameter '{parameterName}'");

            var issues = new List<string>();

            // Remove common formatting characters
            var cleanNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "");

            // Basic format validation
            if (!PhoneRegex().IsMatch(cleanNumber))
                issues.Add("Invalid phone number format");

            // Length validation
            if (cleanNumber.Length < 7 || cleanNumber.Length > 15)
                issues.Add("Phone number length must be between 7-15 digits");

            // Country code validation
            if (requireCountryCode && !cleanNumber.StartsWith('+'))
                issues.Add("Country code is required");

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"Phone validation failed for parameter '{parameterName}'", issues);
        }

        /// <summary>
        /// Validates URL format and safety
        /// </summary>
        public static ValidationResult ValidateUrl(this string? url, string? parameterName = null, string[]? allowedSchemes = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return ValidationResult.Failure($"URL is required for parameter '{parameterName}'");

            var issues = new List<string>();
            allowedSchemes ??= ["http", "https"];

            try
            {
                var uri = new Uri(url);

                // Scheme validation
                if (!allowedSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                    issues.Add($"Scheme '{uri.Scheme}' not allowed. Allowed schemes: {string.Join(", ", allowedSchemes)}");

                // Host validation
                if (string.IsNullOrEmpty(uri.Host))
                    issues.Add("Invalid or missing host");

                // Check for localhost/private IPs if not allowed
                if (IsPrivateOrLocalhost(uri.Host))
                    issues.Add("Private IP addresses or localhost not allowed");

                // Check for suspicious patterns
                if (ContainsSuspiciousUrlPatterns(url))
                    issues.Add("Contains suspicious URL patterns");
            }
            catch (UriFormatException)
            {
                issues.Add("Invalid URL format");
            }

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"URL validation failed for parameter '{parameterName}'", issues);
        }

        #endregion

        #region Password Validation

        /// <summary>
        /// Comprehensive password strength validation
        /// </summary>
        public static PasswordValidationResult ValidatePasswordStrength(this string? password, PasswordPolicy? policy = null)
        {
            policy ??= PasswordPolicy.Default;
            
            if (string.IsNullOrEmpty(password))
                return new PasswordValidationResult { IsValid = false, Issues = ["Password is required"] };

            var issues = new List<string>();
            var score = 0;

            // Length validation
            if (password.Length < policy.MinLength)
                issues.Add($"Password must be at least {policy.MinLength} characters long");
            else if (password.Length >= policy.MinLength)
                score += 1;

            if (password.Length >= policy.RecommendedLength)
                score += 1;

            // Character type requirements
            if (policy.RequireUppercase && !password.Any(char.IsUpper))
                issues.Add("Password must contain at least one uppercase letter");
            else if (password.Any(char.IsUpper))
                score += 1;

            if (policy.RequireLowercase && !password.Any(char.IsLower))
                issues.Add("Password must contain at least one lowercase letter");
            else if (password.Any(char.IsLower))
                score += 1;

            if (policy.RequireDigits && !password.Any(char.IsDigit))
                issues.Add("Password must contain at least one digit");
            else if (password.Any(char.IsDigit))
                score += 1;

            if (policy.RequireSpecialChars && !password.Any(ch => policy.SpecialCharacters.Contains(ch)))
                issues.Add($"Password must contain at least one special character: {string.Join("", policy.SpecialCharacters)}");
            else if (password.Any(ch => policy.SpecialCharacters.Contains(ch)))
                score += 1;

            // Common password check
            if (IsCommonPassword(password))
            {
                issues.Add("Password is too common");
                score -= 2;
            }

            // Repeated characters check
            if (HasTooManyRepeatedCharacters(password))
            {
                issues.Add("Password has too many repeated characters");
                score -= 1;
            }

            // Sequential characters check
            if (HasSequentialCharacters(password))
            {
                issues.Add("Password contains sequential characters");
                score -= 1;
            }

            // Calculate strength
            var strength = score switch
            {
                >= 6 => PasswordStrength.VeryStrong,
                >= 4 => PasswordStrength.Strong,
                >= 2 => PasswordStrength.Medium,
                >= 0 => PasswordStrength.Weak,
                _ => PasswordStrength.VeryWeak
            };

            return new PasswordValidationResult
            {
                IsValid = issues.Count == 0,
                Issues = issues,
                Strength = strength,
                Score = Math.Max(0, score)
            };
        }

        #endregion

        #region Sanitization

        /// <summary>
        /// Sanitizes input to prevent XSS attacks
        /// </summary>
        public static string SanitizeForHtml(this string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // HTML encode the input
            var sanitized = WebUtility.HtmlEncode(input);

            // Additional sanitization for dangerous patterns
            sanitized = RemoveScriptTags(sanitized);
            sanitized = RemoveEventHandlers(sanitized);
            sanitized = RemoveJavaScriptProtocol(sanitized);

            return sanitized;
        }

        /// <summary>
        /// Sanitizes SQL input by removing dangerous characters
        /// </summary>
        public static string SanitizeForSql(this string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove or escape dangerous SQL characters
            return input
                .Replace("'", "''")  // Escape single quotes
                .Replace("--", "")   // Remove SQL comments
                .Replace(";", "")    // Remove statement terminators
                .Replace("/*", "")   // Remove block comment start
                .Replace("*/", "");  // Remove block comment end
        }

        /// <summary>
        /// Sanitizes file path to prevent path traversal
        /// </summary>
        public static string SanitizeFilePath(this string? filePath, bool allowDirectorySeparators = true)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            var sanitized = filePath;

            // Remove null bytes
            sanitized = sanitized.Replace("\0", "");

            // Remove path traversal patterns
            sanitized = sanitized.Replace("..", "");

            // Remove dangerous characters
            var dangerousChars = new char[] { '<', '>', ':', '"', '|', '?', '*' };
            foreach (var ch in dangerousChars)
            {
                sanitized = sanitized.Replace(ch.ToString(), "");
            }

            // Optionally remove directory separators
            if (!allowDirectorySeparators)
            {
                sanitized = sanitized.Replace("/", "").Replace("\\", "");
            }

            return sanitized;
        }

        #endregion

        #region Private Helper Methods

        // SQL Safety Helpers
        private static readonly HashSet<string> DangerousSqlKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "DROP", "DELETE", "INSERT", "UPDATE", "CREATE", "ALTER", "TRUNCATE",
            "EXEC", "EXECUTE", "SP_EXECUTESQL", "XP_CMDSHELL", "OPENROWSET", "OPENQUERY",
            "UNION", "SELECT", "DECLARE", "CAST", "CONVERT", "SUBSTRING",
            "INFORMATION_SCHEMA", "SYSOBJECTS", "SYSCOLUMNS", "MASTER", "MSDB"
        };

        private static bool ContainsDangerousSqlKeywords(string input)
        {
            var upperInput = input.ToUpperInvariant();
            return DangerousSqlKeywords.Any(keyword => upperInput.Contains(keyword));
        }

        private static bool ContainsSqlInjectionPatterns(string input)
        {
            var patterns = new[]
            {
                @"('\s*OR\s*')",
                @"('\s*AND\s*')",
                @"('\s*UNION\s*SELECT)",
                @"(\|\|)",
                @"(--)",
                @"(/\*.*\*/)",
                @"(;\s*DROP)",
                @"(;\s*DELETE)",
                @"('\s*OR\s*1\s*=\s*1)",
                @"('\s*OR\s*'1'\s*=\s*'1')"
            };

            return patterns.Any(pattern => Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        private static bool ContainsDangerousChars(string input)
        {
            var dangerousChars = new[] { '\'', '"', ';', '-', '/', '*', '=', '<', '>', '|', '&' };
            return input.Any(ch => dangerousChars.Contains(ch));
        }

        private static bool ContainsEncodedAttacks(string input)
        {
            var decodedInput = WebUtility.UrlDecode(input);
            return decodedInput != input && (ContainsDangerousSqlKeywords(decodedInput) || ContainsSqlInjectionPatterns(decodedInput));
        }

        // XSS Safety Helpers
        private static bool ContainsScriptTags(string input)
        {
            return Regex.IsMatch(input, @"<\s*script\b[^<]*(?:(?!<\/\s*script\s*>)<[^<]*)*<\/\s*script\s*>", RegexOptions.IgnoreCase);
        }

        private static bool ContainsEventHandlers(string input)
        {
            var eventHandlers = new[]
            {
                "onload", "onclick", "onmouseover", "onerror", "onsubmit", "onchange",
                "onkeydown", "onkeyup", "onmousedown", "onmouseup", "onfocus", "onblur"
            };

            return eventHandlers.Any(handler => 
                Regex.IsMatch(input, $@"\b{handler}\s*=", RegexOptions.IgnoreCase));
        }

        private static bool ContainsJavaScriptProtocol(string input)
        {
            return Regex.IsMatch(input, @"javascript\s*:", RegexOptions.IgnoreCase);
        }

        private static bool ContainsDangerousDataProtocol(string input)
        {
            return Regex.IsMatch(input, @"data\s*:\s*text\/html", RegexOptions.IgnoreCase) ||
                   Regex.IsMatch(input, @"data\s*:\s*.*script", RegexOptions.IgnoreCase);
        }

        private static bool ContainsEncodedXss(string input)
        {
            var decodedInput = WebUtility.HtmlDecode(WebUtility.UrlDecode(input));
            return decodedInput != input && 
                   (ContainsScriptTags(decodedInput) || ContainsEventHandlers(decodedInput) || ContainsJavaScriptProtocol(decodedInput));
        }

        // Path Safety Helpers
        private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs", ".js", ".jar",
            ".asp", ".aspx", ".php", ".jsp", ".py", ".rb", ".pl", ".sh", ".ps1"
        };

        private static bool HasDangerousFileExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return DangerousExtensions.Contains(extension);
        }

        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4",
            "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static bool ContainsReservedNames(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            return ReservedNames.Contains(nameWithoutExtension);
        }

        // URL Safety Helpers
        private static bool IsPrivateOrLocalhost(string host)
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("127.0.0.1") ||
                host.StartsWith("192.168.") ||
                host.StartsWith("10.") ||
                host.StartsWith("172."))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsSuspiciousUrlPatterns(string url)
        {
            var suspiciousPatterns = new[]
            {
                @"data\s*:",
                @"javascript\s*:",
                @"vbscript\s*:",
                @"file\s*:",
                @"ftp\s*://.*@",
                @"\\\\", // UNC paths
                @"\.\./" // Path traversal
            };

            return suspiciousPatterns.Any(pattern => 
                Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase));
        }

        // Password Helpers
        private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "123456", "password123", "admin", "qwerty", "letmein",
            "welcome", "monkey", "1234567890", "abc123", "Password1", "password1",
            "123456789", "welcome123", "admin123", "root", "toor", "pass"
        };

        private static bool IsCommonPassword(string password)
        {
            return CommonPasswords.Contains(password);
        }

        private static bool HasTooManyRepeatedCharacters(string password)
        {
            var consecutiveCount = 1;
            var maxConsecutive = 0;

            for (int i = 1; i < password.Length; i++)
            {
                if (password[i] == password[i - 1])
                {
                    consecutiveCount++;
                }
                else
                {
                    maxConsecutive = Math.Max(maxConsecutive, consecutiveCount);
                    consecutiveCount = 1;
                }
            }

            maxConsecutive = Math.Max(maxConsecutive, consecutiveCount);
            return maxConsecutive > 3; // More than 3 consecutive same characters
        }

        private static bool HasSequentialCharacters(string password)
        {
            var sequences = new[]
            {
                "abcdefghijklmnopqrstuvwxyz",
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                "0123456789",
                "qwertyuiop",
                "asdfghjkl",
                "zxcvbnm"
            };

            foreach (var sequence in sequences)
            {
                for (int i = 0; i <= sequence.Length - 3; i++)
                {
                    var subseq = sequence.Substring(i, 3);
                    if (password.Contains(subseq))
                        return true;
                }
            }

            return false;
        }

        // Sanitization Helpers
        private static string RemoveScriptTags(string input)
        {
            return Regex.Replace(input, @"<\s*script\b[^<]*(?:(?!<\/\s*script\s*>)<[^<]*)*<\/\s*script\s*>", "", RegexOptions.IgnoreCase);
        }

        private static string RemoveEventHandlers(string input)
        {
            return Regex.Replace(input, @"\bon\w+\s*=\s*[""'][^""']*[""']", "", RegexOptions.IgnoreCase);
        }

        private static string RemoveJavaScriptProtocol(string input)
        {
            return Regex.Replace(input, @"javascript\s*:", "", RegexOptions.IgnoreCase);
        }

        #endregion

        #region Regex Patterns

        [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
        public static partial Regex EmailRegex();

        [GeneratedRegex(@"^\+?[\d\s\-\(\)\.]{7,15}$")]
        public static partial Regex PhoneRegex();

        #endregion

        #region Additional (Merged Extra Security Utilities)
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
        #endregion

    }

    #region Supporting Classes

    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public string? ErrorMessage { get; init; }
        public List<string> Issues { get; init; } = new();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failure(string errorMessage, List<string>? issues = null)
        {
            return new()
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                Issues = issues ?? new List<string>()
            };
        }

        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                throw new PowNetValidationException(ErrorMessage ?? "Validation failed")
                    .AddParam("Issues", string.Join(", ", Issues));
            }
        }
    }

    /// <summary>
    /// Password validation result with strength analysis
    /// </summary>
    public class PasswordValidationResult : ValidationResult
    {
        public PasswordStrength Strength { get; init; }
        public int Score { get; init; }
    }

    /// <summary>
    /// Password strength levels
    /// </summary>
    public enum PasswordStrength
    {
        VeryWeak = 0,
        Weak = 1,
        Medium = 2,
        Strong = 3,
        VeryStrong = 4
    }

    /// <summary>
    /// Password policy configuration
    /// </summary>
    public class PasswordPolicy
    {
        public int MinLength { get; set; } = 8;
        public int RecommendedLength { get; set; } = 12;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigits { get; set; } = true;
        public bool RequireSpecialChars { get; set; } = true;
        public HashSet<char> SpecialCharacters { get; set; } = new() 
        { 
            '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', 
            '+', '=', '[', ']', '{', '}', '|', '\\', ':', ';', '"', 
            '\'', '<', '>', ',', '.', '?', '/', '~', '`' 
        };

        public static PasswordPolicy Default => new();

        public static PasswordPolicy Strict => new()
        {
            MinLength = 12,
            RecommendedLength = 16,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireDigits = true,
            RequireSpecialChars = true
        };

        public static PasswordPolicy Relaxed => new()
        {
            MinLength = 6,
            RecommendedLength = 8,
            RequireUppercase = false,
            RequireLowercase = true,
            RequireDigits = true,
            RequireSpecialChars = false
        };
    }

    #endregion
}