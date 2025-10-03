using System.Security.Cryptography;
using System.Text;
using PowNet.Common;

namespace PowNet.Extensions
{
    /// <summary>
    /// Advanced encryption and cryptography extensions for PowNet framework
    /// </summary>
    public static class EncryptionExtensions
    {
        #region AES Encryption

        /// <summary>
        /// Encrypt string using AES-256-GCM (Authenticated Encryption)
        /// </summary>
        public static string EncryptAesGcm(this string plaintext, string key)
        {
            if (string.IsNullOrEmpty(plaintext))
                throw new PowNetSecurityException("Plaintext cannot be null or empty");

            if (string.IsNullOrEmpty(key))
                throw new PowNetSecurityException("Encryption key cannot be null or empty");

            try
            {
                var keyBytes = DeriveKey(key, 32); // AES-256 key
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

                using var aes = new AesGcm(keyBytes);

                var nonce = new byte[12]; // 96-bit nonce for GCM
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[16]; // 128-bit authentication tag

                RandomNumberGenerator.Fill(nonce);

                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                // Combine nonce + ciphertext + tag
                var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
                Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
                Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                throw new PowNetSecurityException("Encryption failed", "AES_GCM_ENCRYPT")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        /// <summary>
        /// Decrypt string using AES-256-GCM (Authenticated Encryption)
        /// </summary>
        public static string DecryptAesGcm(this string ciphertext, string key)
        {
            if (string.IsNullOrEmpty(ciphertext))
                throw new PowNetSecurityException("Ciphertext cannot be null or empty");

            if (string.IsNullOrEmpty(key))
                throw new PowNetSecurityException("Decryption key cannot be null or empty");

            try
            {
                var keyBytes = DeriveKey(key, 32); // AES-256 key
                var encryptedData = Convert.FromBase64String(ciphertext);

                if (encryptedData.Length < 28) // nonce(12) + tag(16) = minimum 28 bytes
                    throw new PowNetSecurityException("Invalid ciphertext format");

                var nonce = new byte[12];
                var tag = new byte[16];
                var encryptedBytes = new byte[encryptedData.Length - 28];

                Buffer.BlockCopy(encryptedData, 0, nonce, 0, 12);
                Buffer.BlockCopy(encryptedData, 12, encryptedBytes, 0, encryptedBytes.Length);
                Buffer.BlockCopy(encryptedData, 12 + encryptedBytes.Length, tag, 0, 16);

                using var aes = new AesGcm(keyBytes);
                var decryptedBytes = new byte[encryptedBytes.Length];

                aes.Decrypt(nonce, encryptedBytes, tag, decryptedBytes);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex) when (!(ex is PowNetSecurityException))
            {
                throw new PowNetSecurityException("Decryption failed", "AES_GCM_DECRYPT")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        /// <summary>
        /// Legacy AES-CBC encryption for backward compatibility
        /// </summary>
        public static string Encode(this string plaintext, string key)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            if (string.IsNullOrEmpty(key))
                throw new PowNetSecurityException("Encryption key cannot be null or empty");

            try
            {
                var keyBytes = DeriveKey(key, 32);
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

                using var aes = Aes.Create();
                aes.Key = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                var encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

                // Combine IV + encrypted data
                var result = new byte[aes.IV.Length + encryptedBytes.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                throw new PowNetSecurityException("Legacy encryption failed", "AES_CBC_ENCRYPT")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        /// <summary>
        /// Legacy AES-CBC decryption for backward compatibility
        /// </summary>
        public static string Decode(this string ciphertext, string key)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return string.Empty;

            if (string.IsNullOrEmpty(key))
                throw new PowNetSecurityException("Decryption key cannot be null or empty");

            try
            {
                var keyBytes = DeriveKey(key, 32);
                var encryptedData = Convert.FromBase64String(ciphertext);

                if (encryptedData.Length < 16) // Minimum IV size
                    throw new PowNetSecurityException("Invalid ciphertext format");

                using var aes = Aes.Create();
                aes.Key = keyBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var iv = new byte[16];
                var encrypted = new byte[encryptedData.Length - 16];
                Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
                Buffer.BlockCopy(encryptedData, 16, encrypted, 0, encrypted.Length);

                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                var decryptedBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex) when (!(ex is PowNetSecurityException))
            {
                throw new PowNetSecurityException("Legacy decryption failed", "AES_CBC_DECRYPT")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        #endregion

        #region RSA Encryption

        /// <summary>
        /// Generate RSA key pair
        /// </summary>
        public static RSAKeyPair GenerateRSAKeyPair(int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);

            return new RSAKeyPair
            {
                PublicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey()),
                PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
                KeySize = keySize
            };
        }

        /// <summary>
        /// Encrypt using RSA public key
        /// </summary>
        public static string EncryptRSA(this string plaintext, string publicKey)
        {
            if (string.IsNullOrEmpty(plaintext))
                throw new PowNetSecurityException("Plaintext cannot be null or empty");

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);

                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var encryptedBytes = rsa.Encrypt(plaintextBytes, RSAEncryptionPadding.OaepSHA256);

                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                throw new PowNetSecurityException("RSA encryption failed", "RSA_ENCRYPT")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        /// <summary>
        /// Decrypt using RSA private key
        /// </summary>
        public static string DecryptRSA(this string ciphertext, string privateKey)
        {
            if (string.IsNullOrEmpty(ciphertext))
                throw new PowNetSecurityException("Ciphertext cannot be null or empty");

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);

                var encryptedBytes = Convert.FromBase64String(ciphertext);
                var decryptedBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new PowNetSecurityException("RSA decryption failed", "RSA_DECRYPT")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        #endregion

        #region Digital Signatures

        /// <summary>
        /// Sign data using RSA private key
        /// </summary>
        public static string SignRSA(this string data, string privateKey)
        {
            if (string.IsNullOrEmpty(data))
                throw new PowNetSecurityException("Data to sign cannot be null or empty");

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);

                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return Convert.ToBase64String(signatureBytes);
            }
            catch (Exception ex)
            {
                throw new PowNetSecurityException("RSA signing failed", "RSA_SIGN")
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        /// <summary>
        /// Verify RSA signature
        /// </summary>
        public static bool VerifyRSASignature(this string data, string signature, string publicKey)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(signature))
                return false;

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);

                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signature);

                return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Hashing

        /// <summary>
        /// Compute SHA-256 hash
        /// </summary>
        public static string ComputeSHA256(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Compute SHA-512 hash
        /// </summary>
        public static string ComputeSHA512(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            using var sha512 = SHA512.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha512.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Compute HMAC-SHA256
        /// </summary>
        public static string ComputeHMAC(this string input, string key)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var keyBytes = DeriveKey(key, 32);
            using var hmac = new HMACSHA256(keyBytes);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = hmac.ComputeHash(inputBytes);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verify HMAC signature
        /// </summary>
        public static bool VerifyHMAC(this string input, string signature, string key)
        {
            var computedSignature = input.ComputeHMAC(key);
            return SecureStringCompare(computedSignature, signature);
        }

        #endregion

        #region Key Derivation

        /// <summary>
        /// Derive key using PBKDF2
        /// </summary>
        public static byte[] DeriveKey(string password, int keyLength, byte[]? salt = null, int iterations = 100000)
        {
            salt ??= Encoding.UTF8.GetBytes("PowNet-Salt"); // Default salt (not recommended for production)

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(keyLength);
        }

        /// <summary>
        /// Generate cryptographically secure random bytes
        /// </summary>
        public static byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }

        /// <summary>
        /// Generate cryptographically secure random string
        /// </summary>
        public static string GenerateRandomString(int length, string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        {
            var result = new StringBuilder(length);
            var bytes = GenerateRandomBytes(length * 4); // Extra bytes for modulo operation

            for (int i = 0; i < length; i++)
            {
                var value = BitConverter.ToUInt32(bytes, i * 4);
                result.Append(charset[(int)(value % (uint)charset.Length)]);
            }

            return result.ToString();
        }

        #endregion

        #region File Encryption

        /// <summary>
        /// Encrypt file using AES-GCM
        /// </summary>
        public static void EncryptFile(string inputFilePath, string outputFilePath, string key)
        {
            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("Input file not found");

            try
            {
                var keyBytes = DeriveKey(key, 32);
                var plaintextBytes = File.ReadAllBytes(inputFilePath);

                using var aes = new AesGcm(keyBytes);

                var nonce = new byte[12];
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[16];

                RandomNumberGenerator.Fill(nonce);

                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                using var outputStream = File.Create(outputFilePath);
                outputStream.Write(nonce);
                outputStream.Write(ciphertext);
                outputStream.Write(tag);
            }
            catch (Exception ex)
            {
                throw new PowNetSecurityException("File encryption failed", "FILE_ENCRYPT")
                    .AddParam("InputFile", inputFilePath)
                    .AddParam("OutputFile", outputFilePath)
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        /// <summary>
        /// Decrypt file using AES-GCM
        /// </summary>
        public static void DecryptFile(string inputFilePath, string outputFilePath, string key)
        {
            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("Input file not found");

            try
            {
                var keyBytes = DeriveKey(key, 32);
                var encryptedData = File.ReadAllBytes(inputFilePath);

                if (encryptedData.Length < 28)
                    throw new PowNetSecurityException("Invalid encrypted file format");

                var nonce = new byte[12];
                var tag = new byte[16];
                var ciphertext = new byte[encryptedData.Length - 28];

                Buffer.BlockCopy(encryptedData, 0, nonce, 0, 12);
                Buffer.BlockCopy(encryptedData, 12, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(encryptedData, 12 + ciphertext.Length, tag, 0, 16);

                using var aes = new AesGcm(keyBytes);
                var plaintext = new byte[ciphertext.Length];

                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                File.WriteAllBytes(outputFilePath, plaintext);
            }
            catch (Exception ex) when (!(ex is PowNetSecurityException))
            {
                throw new PowNetSecurityException("File decryption failed", "FILE_DECRYPT")
                    .AddParam("InputFile", inputFilePath)
                    .AddParam("OutputFile", outputFilePath)
                    .AddParam("ErrorType", ex.GetType().Name)
                    .AddParam("ErrorMessage", ex.Message);
            }
        }

        #endregion

        #region Private Helper Methods

        private static bool SecureStringCompare(string a, string b)
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
    }

    #region Supporting Classes

    /// <summary>
    /// RSA key pair container
    /// </summary>
    public class RSAKeyPair
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public int KeySize { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Export public key in PEM format
        /// </summary>
        public string ExportPublicKeyPem()
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(PublicKey), out _);
            return rsa.ExportRSAPublicKeyPem();
        }

        /// <summary>
        /// Export private key in PEM format
        /// </summary>
        public string ExportPrivateKeyPem()
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(PrivateKey), out _);
            return rsa.ExportRSAPrivateKeyPem();
        }
    }

    /// <summary>
    /// Encryption context for tracking encryption operations
    /// </summary>
    public class EncryptionContext
    {
        public string Algorithm { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string KeyId { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    #endregion
}