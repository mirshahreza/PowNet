using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using PowNet.Common;
using PowNet.Configuration;

namespace PowNet.Extensions
{
    /// <summary>
    /// Authentication and authorization extensions for PowNet framework
    /// </summary>
    public static class AuthExtensions
    {
        #region Password Hashing

        /// <summary>
        /// Hash password using PBKDF2 with salt
        /// </summary>
        public static HashedPassword HashPassword(this string password, int iterations = 100000)
        {
            if (string.IsNullOrEmpty(password))
                throw new PowNetSecurityException("Password cannot be null or empty");

            var salt = RandomNumberGenerator.GetBytes(32);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32);

            return new HashedPassword
            {
                Hash = Convert.ToBase64String(hash),
                Salt = Convert.ToBase64String(salt),
                Iterations = iterations,
                Algorithm = "PBKDF2-SHA256"
            };
        }

        /// <summary>
        /// Verify password against hash
        /// </summary>
        public static bool VerifyPassword(this string password, HashedPassword hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || hashedPassword == null)
                return false;

            try
            {
                var salt = Convert.FromBase64String(hashedPassword.Salt);
                var expected = Convert.FromBase64String(hashedPassword.Hash);
                var computed = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    hashedPassword.Iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);
                return SecureStringCompare(Convert.ToBase64String(computed), hashedPassword.Hash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generate secure random password
        /// </summary>
        public static string GenerateSecurePassword(int length = 16, bool includeSpecialChars = true)
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string special = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var charset = lowercase + uppercase + digits;
            if (includeSpecialChars)
                charset += special;

            var password = new StringBuilder(length);
            using var rng = RandomNumberGenerator.Create();

            // Ensure at least one character from each required set
            password.Append(GetRandomChar(lowercase, rng));
            password.Append(GetRandomChar(uppercase, rng));
            password.Append(GetRandomChar(digits, rng));
            if (includeSpecialChars)
                password.Append(GetRandomChar(special, rng));

            // Fill the rest randomly
            for (int i = password.Length; i < length; i++)
            {
                password.Append(GetRandomChar(charset, rng));
            }

            // Shuffle the password
            return ShuffleString(password.ToString(), rng);
        }

        #endregion

        #region JWT Token Management

        private static SymmetricSecurityKey GetJwtSigningKey()
        {
            var secret = PowNetConfiguration.EncryptionSecret;
            var keyBytes = EncryptionExtensions.DeriveKey(secret, 32);
            return new SymmetricSecurityKey(keyBytes);
        }

        /// <summary>
        /// Generate JWT token with claims
        /// </summary>
        public static string GenerateJwtToken(this ClaimsPrincipal principal, TimeSpan? expiration = null, string? audience = null)
        {
            var key = GetJwtSigningKey();
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "PowNet",
                audience: audience ?? "PowNet-Client",
                claims: principal.Claims,
                expires: DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(24)),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validate and parse JWT token
        /// </summary>
        public static ClaimsPrincipal? ValidateJwtToken(this string token, string? audience = null)
        {
            try
            {
                var key = GetJwtSigningKey();

                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = "PowNet",
                    ValidateAudience = true,
                    ValidAudience = audience ?? "PowNet-Client",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extract claims from JWT token without validation (for debugging)
        /// </summary>
        public static IEnumerable<Claim> ExtractClaims(this string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);
                return jsonToken.Claims;
            }
            catch
            {
                return Enumerable.Empty<Claim>();
            }
        }

        #endregion

        #region API Key Management

        /// <summary>
        /// Generate secure API key
        /// </summary>
        public static string GenerateApiKey(int length = 32)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            using var rng = RandomNumberGenerator.Create();
            var result = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                result.Append(GetRandomChar(chars, rng));
            }

            return result.ToString();
        }

        /// <summary>
        /// Hash API key for storage
        /// </summary>
        public static string HashApiKey(this string apiKey)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToBase64String(hashedBytes);
        }

        /// <summary>
        /// Verify API key against hash
        /// </summary>
        public static bool VerifyApiKey(this string apiKey, string hashedApiKey)
        {
            var computedHash = apiKey.HashApiKey();
            return SecureStringCompare(computedHash, hashedApiKey);
        }

        #endregion

        #region Rate Limiting

        public static bool IsWithinRateLimit(this string identifier, int maxRequests, TimeSpan window, string? category = null)
        {
            var rateLimiter = RateLimiterManager.GetOrCreate(identifier, category);
            return rateLimiter.IsAllowed(maxRequests, window);
        }

        public static void RecordRequest(this string identifier, string? category = null)
        {
            var rateLimiter = RateLimiterManager.GetOrCreate(identifier, category);
            rateLimiter.RecordRequest();
        }

        public static int GetRemainingRequests(this string identifier, int maxRequests, TimeSpan window, string? category = null)
        {
            var rateLimiter = RateLimiterManager.GetOrCreate(identifier, category);
            return rateLimiter.GetRemainingRequests(maxRequests, window);
        }

        #endregion

        #region Authorization Helpers

        public static bool HasRole(this ClaimsPrincipal principal, string role) => principal?.IsInRole(role) == true;
        public static bool HasAnyRole(this ClaimsPrincipal principal, params string[] roles) => roles.Any(role => principal?.IsInRole(role) == true);
        public static bool HasAllRoles(this ClaimsPrincipal principal, params string[] roles) => roles.All(role => principal?.IsInRole(role) == true);

        public static string? GetUserId(this ClaimsPrincipal principal) =>
            principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            principal?.FindFirst("sub")?.Value ??
            principal?.FindFirst("user_id")?.Value;

        public static string? GetUserName(this ClaimsPrincipal principal) =>
            principal?.FindFirst(ClaimTypes.Name)?.Value ??
            principal?.FindFirst("username")?.Value ??
            principal?.FindFirst("preferred_username")?.Value;

        public static string? GetEmail(this ClaimsPrincipal principal) =>
            principal?.FindFirst(ClaimTypes.Email)?.Value ??
            principal?.FindFirst("email")?.Value;

        #endregion

        #region Security Headers

        public static string GenerateCSPHeader(CSPPolicy? policy = null)
        {
            policy ??= CSPPolicy.Default;
            var directives = new List<string>();
            if (policy.DefaultSrc.Any()) directives.Add($"default-src {string.Join(" ", policy.DefaultSrc)}");
            if (policy.ScriptSrc.Any()) directives.Add($"script-src {string.Join(" ", policy.ScriptSrc)}");
            if (policy.StyleSrc.Any()) directives.Add($"style-src {string.Join(" ", policy.StyleSrc)}");
            if (policy.ImgSrc.Any()) directives.Add($"img-src {string.Join(" ", policy.ImgSrc)}");
            if (policy.ConnectSrc.Any()) directives.Add($"connect-src {string.Join(" ", policy.ConnectSrc)}");
            if (policy.FontSrc.Any()) directives.Add($"font-src {string.Join(" ", policy.FontSrc)}");
            if (policy.ObjectSrc.Any()) directives.Add($"object-src {string.Join(" ", policy.ObjectSrc)}");
            if (policy.MediaSrc.Any()) directives.Add($"media-src {string.Join(" ", policy.MediaSrc)}");
            if (policy.FrameSrc.Any()) directives.Add($"frame-src {string.Join(" ", policy.FrameSrc)}");
            return string.Join("; ", directives);
        }

        #endregion

        #region Private Helper Methods

        private static char GetRandomChar(string charset, RandomNumberGenerator rng)
        {
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var value = BitConverter.ToUInt32(bytes, 0);
            return charset[(int)(value % (uint)charset.Length)];
        }

        private static string ShuffleString(string input, RandomNumberGenerator rng)
        {
            var array = input.ToCharArray();
            for (int i = array.Length - 1; i > 0; i--)
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var j = (int)(BitConverter.ToUInt32(bytes, 0) % (uint)(i + 1));
                (array[i], array[j]) = (array[j], array[i]);
            }
            return new string(array);
        }

        private static bool SecureStringCompare(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var result = 0;
            for (int i = 0; i < a.Length; i++) result |= a[i] ^ b[i];
            return result == 0;
        }

        #endregion
    }

    #region Supporting Classes
    public class HashedPassword
    {
        public string Hash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public string Algorithm { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CSPPolicy
    {
        public List<string> DefaultSrc { get; set; } = new();
        public List<string> ScriptSrc { get; set; } = new();
        public List<string> StyleSrc { get; set; } = new();
        public List<string> ImgSrc { get; set; } = new();
        public List<string> ConnectSrc { get; set; } = new();
        public List<string> FontSrc { get; set; } = new();
        public List<string> ObjectSrc { get; set; } = new();
        public List<string> MediaSrc { get; set; } = new();
        public List<string> FrameSrc { get; set; } = new();

        public static CSPPolicy Default => new()
        {
            DefaultSrc = ["'self'"],
            ScriptSrc = ["'self'", "'unsafe-inline'"],
            StyleSrc = ["'self'", "'unsafe-inline'"],
            ImgSrc = ["'self'", "data:", "https:"],
            ConnectSrc = ["'self'"],
            FontSrc = ["'self'"],
            ObjectSrc = ["'none'"],
            MediaSrc = ["'self'"],
            FrameSrc = ["'none'"]
        };

        public static CSPPolicy Strict => new()
        {
            DefaultSrc = ["'self'"],
            ScriptSrc = ["'self'"],
            StyleSrc = ["'self'"],
            ImgSrc = ["'self'"],
            ConnectSrc = ["'self'"],
            FontSrc = ["'self'"],
            ObjectSrc = ["'none'"],
            MediaSrc = ["'none'"],
            FrameSrc = ["'none'"]
        };
    }

    public class RateLimiter
    {
        private readonly Queue<DateTime> _requests = new();
        private readonly object _lock = new();
        public bool IsAllowed(int maxRequests, TimeSpan window)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow - window;
                while (_requests.Count > 0 && _requests.Peek() < cutoff) _requests.Dequeue();
                return _requests.Count < maxRequests;
            }
        }
        public void RecordRequest() { lock (_lock) { _requests.Enqueue(DateTime.UtcNow); } }
        public int GetRemainingRequests(int maxRequests, TimeSpan window)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow - window;
                while (_requests.Count > 0 && _requests.Peek() < cutoff) _requests.Dequeue();
                return Math.Max(0, maxRequests - _requests.Count);
            }
        }
    }

    public static class RateLimiterManager
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RateLimiter> _limiters = new();
        public static RateLimiter GetOrCreate(string identifier, string? category = null)
        {
            var key = category != null ? $"{category}:{identifier}" : identifier;
            return _limiters.GetOrAdd(key, _ => new RateLimiter());
        }
        public static void Remove(string identifier, string? category = null)
        {
            var key = category != null ? $"{category}:{identifier}" : identifier;
            _limiters.TryRemove(key, out _);
        }
        public static void Clear() => _limiters.Clear();
        public static int GetActiveLimitersCount() => _limiters.Count;
    }
    #endregion
}