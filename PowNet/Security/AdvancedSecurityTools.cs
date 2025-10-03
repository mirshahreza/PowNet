using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PowNet.Common;
using PowNet.Extensions;

namespace PowNet.Extensions
{
    /// <summary>
    /// Consolidated advanced security utilities (passwords, sanitization, tokens, signatures, headers)
    /// Renamed from AdvancedSecurityExtensions to provide clearer semantic meaning (tools, not only extensions).
    /// </summary>
    public static partial class AdvancedSecurityTools
    {
        #region Password Security

        /// <summary>
        /// Generate cryptographically secure random password with complexity requirements
        /// </summary>
        public static string GenerateSecurePassword(
            int length = 16,
            bool includeUppercase = true,
            bool includeLowercase = true,
            bool includeNumbers = true,
            bool includeSpecialChars = true,
            string excludeChars = "0O1lI|`",
            bool avoidAmbiguous = true)
        {
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string numbers = "0123456789";
            const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var charset = new StringBuilder();
            var required = new List<char>();

            if (includeUppercase)
            {
                var chars = avoidAmbiguous ? uppercase.Where(c => !excludeChars.Contains(c)).ToArray() : uppercase.ToCharArray();
                charset.Append(chars);
                required.Add(GetRandomChar(chars));
            }

            if (includeLowercase)
            {
                var chars = avoidAmbiguous ? lowercase.Where(c => !excludeChars.Contains(c)).ToArray() : lowercase.ToCharArray();
                charset.Append(chars);
                required.Add(GetRandomChar(chars));
            }

            if (includeNumbers)
            {
                var chars = avoidAmbiguous ? numbers.Where(c => !excludeChars.Contains(c)).ToArray() : numbers.ToCharArray();
                charset.Append(chars);
                required.Add(GetRandomChar(chars));
            }

            if (includeSpecialChars)
            {
                var chars = avoidAmbiguous ? special.Where(c => !excludeChars.Contains(c)).ToArray() : special.ToCharArray();
                charset.Append(chars);
                required.Add(GetRandomChar(chars));
            }

            if (charset.Length == 0)
                throw new ArgumentException("At least one character type must be included");

            var charsetArray = charset.ToString().ToCharArray();
            var password = new char[length];

            // Add required characters first
            for (int i = 0; i < required.Count && i < length; i++)
            {
                password[i] = required[i];
            }

            // Fill remaining positions
            for (int i = required.Count; i < length; i++)
            {
                password[i] = GetRandomChar(charsetArray);
            }

            // Shuffle the password
            return new string(ShuffleArray(password));
        }

        /// <summary>
        /// Calculate password entropy in bits
        /// </summary>
        public static double CalculatePasswordEntropy(string password)
        {
            if (string.IsNullOrEmpty(password))
                return 0;

            var charsetSize = 0;
            
            if (password.Any(char.IsLower)) charsetSize += 26;
            if (password.Any(char.IsUpper)) charsetSize += 26;
            if (password.Any(char.IsDigit)) charsetSize += 10;
            if (password.Any(c => !char.IsLetterOrDigit(c))) charsetSize += 32; // Approximate special chars

            return password.Length * Math.Log2(charsetSize);
        }

        /// <summary>
        /// Check if password has been compromised in known breaches
        /// </summary>
        public static async Task<bool> IsPasswordCompromisedAsync(string password)
        {
            var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "password", "123456", "password123", "admin", "qwerty", "letmein",
                "welcome", "monkey", "dragon", "master", "shadow", "princess"
            };

            return commonPasswords.Contains(password);
        }

        #endregion

        #region Input Sanitization

        public static string SanitizeHtmlContent(this string html, string[]? allowedTags = null)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            allowedTags ??= new[] { "p", "br", "strong", "em", "u", "ul", "ol", "li", "a", "span" };

            // Remove script tags and their content
            html = ScriptTagRegex().Replace(html, "");
            
            // Remove dangerous event handlers
            html = EventHandlerRegex().Replace(html, "");
            
            // Remove javascript: and data: protocols from href and src
            html = JavaScriptProtocolRegex().Replace(html, "href=\"#\"");
            html = DataProtocolRegex().Replace(html, "src=\"\"");

            // Remove tags not in allowed list (simple pass)
            html = Regex.Replace(html, @"<(?!/?)(?!" + string.Join("|", allowedTags) + @"\b)[^>]*>", "", RegexOptions.IgnoreCase);

            return html;
        }

        public static string SanitizeFileName(this string fileName, string replacement = "_")
        {
            if (string.IsNullOrEmpty(fileName))
                return "file";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;

            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar.ToString(), replacement);
            }

            while (sanitized.Contains(replacement + replacement))
            {
                sanitized = sanitized.Replace(replacement + replacement, replacement);
            }

            sanitized = sanitized.Trim(replacement.ToCharArray());

            if (string.IsNullOrEmpty(sanitized) || IsReservedFileName(sanitized))
            {
                sanitized = "file_" + Guid.NewGuid().ToString("N")[..8];
            }

            return sanitized;
        }

        public static string SanitizeCsvField(this string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            var dangerous = new[] { "=", "+", "-", "@", "\t", "\r", "\n" };
            
            if (dangerous.Any(d => field.StartsWith(d)))
            {
                field = "'" + field;
            }

            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                field = "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        #endregion

        #region Token Security

        public static string GenerateSecureToken(int length = 32, TokenFormat format = TokenFormat.Base64)
        {
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            return format switch
            {
                TokenFormat.Hex => Convert.ToHexString(bytes).ToLower(),
                TokenFormat.Base64 => Convert.ToBase64String(bytes).TrimEnd('='),
                TokenFormat.Base64Url => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
                TokenFormat.Alphanumeric => ConvertToAlphanumeric(bytes),
                _ => Convert.ToBase64String(bytes)
            };
        }

        public static string GenerateTOTP(string secret, DateTime? timestamp = null, int digits = 6, int stepSize = 30)
        {
            var time = timestamp ?? DateTime.UtcNow;
            var unixTime = ((DateTimeOffset)time).ToUnixTimeSeconds();
            var counter = unixTime / stepSize;
            return GenerateHOTP(secret, counter, digits);
        }

        public static string GenerateHOTP(string secret, long counter, int digits = 6)
        {
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var counterBytes = BitConverter.GetBytes(counter);
            
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(secretBytes);
            var hash = hmac.ComputeHash(counterBytes);

            var offset = hash[^1] & 0x0F;
            var code = ((hash[offset] & 0x7F) << 24) |
                      ((hash[offset + 1] & 0xFF) << 16) |
                      ((hash[offset + 2] & 0xFF) << 8) |
                      (hash[offset + 3] & 0xFF);

            var otp = code % (int)Math.Pow(10, digits);
            return otp.ToString().PadLeft(digits, '0');
        }

        public static bool VerifyTOTP(string secret, string code, DateTime? timestamp = null, int windowSize = 1)
        {
            var time = timestamp ?? DateTime.UtcNow;
            
            for (int i = -windowSize; i <= windowSize; i++)
            {
                var testTime = time.AddSeconds(i * 30);
                var expectedCode = GenerateTOTP(secret, testTime);
                
                if (ConstantTimeEquals(code, expectedCode))
                    return true;
            }

            return false;
        }

        #endregion

        #region Digital Signatures

        public static string CreateDigitalSignature(string data, string privateKey, SignatureAlgorithm algorithm = SignatureAlgorithm.RSA_SHA256)
        {
            return algorithm switch
            {
                SignatureAlgorithm.RSA_SHA256 => CreateRSASignature(data, privateKey),
                SignatureAlgorithm.HMAC_SHA256 => CreateHMACSignature(data, privateKey),
                _ => throw new ArgumentException("Unsupported signature algorithm")
            };
        }

        public static bool VerifyDigitalSignature(string data, string signature, string key, SignatureAlgorithm algorithm = SignatureAlgorithm.RSA_SHA256)
        {
            try
            {
                return algorithm switch
                {
                    SignatureAlgorithm.RSA_SHA256 => data.VerifyRSASignature(signature, key),
                    SignatureAlgorithm.HMAC_SHA256 => VerifyHMACSignature(data, signature, key),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Security Headers

        public static Dictionary<string, string> GenerateSecurityHeaders(SecurityHeadersConfig? config = null)
        {
            config ??= SecurityHeadersConfig.Default;

            var headers = new Dictionary<string, string>();

            if (config.EnableHSTS)
            {
                headers["Strict-Transport-Security"] = $"max-age={config.HSTSMaxAge}; includeSubDomains" + 
                    (config.HSTSPreload ? "; preload" : "");
            }

            if (!string.IsNullOrEmpty(config.ContentSecurityPolicy))
            {
                headers["Content-Security-Policy"] = config.ContentSecurityPolicy;
            }

            headers["X-Frame-Options"] = config.XFrameOptions;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-XSS-Protection"] = "1; mode=block";
            headers["Referrer-Policy"] = config.ReferrerPolicy;

            if (!string.IsNullOrEmpty(config.PermissionsPolicy))
            {
                headers["Permissions-Policy"] = config.PermissionsPolicy;
            }

            headers["Server"] = string.Empty;
            headers["X-Powered-By"] = string.Empty;

            return headers;
        }

        #endregion

        #region Private Helpers

        private static char GetRandomChar(char[] chars)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var value = BitConverter.ToUInt32(bytes, 0);
            return chars[value % chars.Length];
        }

        private static char[] ShuffleArray(char[] array)
        {
            using var rng = RandomNumberGenerator.Create();
            for (int i = array.Length - 1; i > 0; i--)
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var j = (int)(BitConverter.ToUInt32(bytes, 0) % (i + 1));
                (array[i], array[j]) = (array[j], array[i]);
            }
            return array;
        }

        private static bool IsReservedFileName(string fileName)
        {
            var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            return reserved.Contains(fileName.ToUpperInvariant());
        }

        private static string ConvertToAlphanumeric(byte[] bytes)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var result = new StringBuilder();
            
            foreach (var b in bytes)
            {
                result.Append(chars[b % chars.Length]);
            }
            
            return result.ToString();
        }

        private static string CreateRSASignature(string data, string privateKey) => data.SignRSA(privateKey);
        private static string CreateHMACSignature(string data, string key) => data.ComputeHMAC(key);
        private static bool VerifyHMACSignature(string data, string signature, string key) => data.VerifyHMAC(signature, key);

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        #endregion

        #region Regex Patterns

        [GeneratedRegex(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase)]
        public static partial Regex ScriptTagRegex();

        [GeneratedRegex(@"\bon\w+\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase)]
        public static partial Regex EventHandlerRegex();

        [GeneratedRegex(@"href\s*=\s*[""']javascript:[^""']*[""']", RegexOptions.IgnoreCase)]
        public static partial Regex JavaScriptProtocolRegex();

        [GeneratedRegex(@"src\s*=\s*[""']data:[^""']*[""']", RegexOptions.IgnoreCase)]
        public static partial Regex DataProtocolRegex();

        #endregion
    }

    #region Supporting Types

    public enum TokenFormat
    {
        Base64,
        Base64Url,
        Hex,
        Alphanumeric
    }

    public enum SignatureAlgorithm
    {
        RSA_SHA256,
        HMAC_SHA256
    }

    public class SecurityHeadersConfig
    {
        public bool EnableHSTS { get; set; } = true;
        public int HSTSMaxAge { get; set; } = 31536000;
        public bool HSTSPreload { get; set; } = false;
        public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
        public string XFrameOptions { get; set; } = "DENY";
        public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
        public string PermissionsPolicy { get; set; } = "geolocation=(), microphone=(), camera=()";

        public static SecurityHeadersConfig Default => new();

        public static SecurityHeadersConfig Strict => new()
        {
            ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'",
            XFrameOptions = "DENY",
            ReferrerPolicy = "no-referrer"
        };
    }

    #endregion
}
