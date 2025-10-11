using System.Security.Cryptography;
using System.Text;

namespace PowNet.Extensions
{
    /// <summary>
    /// Modern password hashing (PBKDF2-SHA256) with self-describing format:
    /// PBKDF2-SHA256$<iterations>$<saltBase64>$<hashBase64>
    /// </summary>
    public static class ModernHashExtensions
    {
        /// <summary>
        /// Generate a modern salted hash for the input (intended for passwords / secrets).
        /// Format: PBKDF2-SHA256$iterations$salt$hash
        /// </summary>
        public static string GetHash(this string input, int iterations = 100_000, int saltSize = 32, int hashSize = 32)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty", nameof(input));

            var salt = RandomNumberGenerator.GetBytes(saltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(input),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                hashSize);

            return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verify input against a stored hash produced by GetHash.
        /// </summary>
        public static bool VerifyHash(this string input, string stored)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(stored))
                return false;

            var parts = stored.Split('$');
            if (parts.Length != 4) return false;
            if (!parts[0].Equals("PBKDF2-SHA256", StringComparison.OrdinalIgnoreCase)) return false;
            if (!int.TryParse(parts[1], out var iterations)) return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expectedBytes = Convert.FromBase64String(parts[3]);

                var computed = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(input),
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedBytes.Length);

                return CryptographicOperations.FixedTimeEquals(computed, expectedBytes);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extract iteration count from stored hash (if valid).
        /// </summary>
        public static int? GetHashIterations(this string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            var parts = stored.Split('$');
            if (parts.Length == 4 && int.TryParse(parts[1], out var it)) return it;
            return null;
        }
    }
}
