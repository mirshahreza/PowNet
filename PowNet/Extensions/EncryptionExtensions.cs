using System.Security.Cryptography;
using System.Text;
using PowNet.Common;

namespace PowNet.Extensions
{
    /// <summary>
    /// Cryptography extensions (AES-256-GCM, RSA, hashing, HMAC, PBKDF2, random utilities, file encryption)
    /// </summary>
    public static class EncryptionExtensions
    {
        #region AES Encryption (GCM)

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

                var nonce = new byte[12]; // 96-bit nonce
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[16];   // 128-bit tag

                RandomNumberGenerator.Fill(nonce);
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

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
        /// Decrypt string using AES-256-GCM
        /// </summary>
        public static string DecryptAesGcm(this string ciphertext, string key)
        {
            if (string.IsNullOrEmpty(ciphertext))
                throw new PowNetSecurityException("Ciphertext cannot be null or empty");

            if (string.IsNullOrEmpty(key))
                throw new PowNetSecurityException("Decryption key cannot be null or empty");

            try
            {
                var keyBytes = DeriveKey(key, 32);
                var encryptedData = Convert.FromBase64String(ciphertext);
                if (encryptedData.Length < 28)
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

        #endregion

        #region RSA Encryption
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
            catch { return false; }
        }
        #endregion

        #region Hashing / HMAC
        public static string ComputeSHA256(this string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(sha256.ComputeHash(bytes));
        }
        public static string ComputeSHA512(this string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            using var sha512 = SHA512.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(sha512.ComputeHash(bytes));
        }
        public static string ComputeHMAC(this string input, string key)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var keyBytes = DeriveKey(key, 32);
            using var hmac = new HMACSHA256(keyBytes);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(hmac.ComputeHash(inputBytes));
        }
        public static bool VerifyHMAC(this string input, string signature, string key)
        {
            var computedSignature = input.ComputeHMAC(key);
            return SecureStringCompare(computedSignature, signature);
        }
        #endregion

        #region Key Derivation / Random
        public static byte[] DeriveKey(string password, int keyLength, byte[]? salt = null, int iterations = 100000)
        {
            salt ??= Encoding.UTF8.GetBytes("PowNet-Salt");
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                keyLength);
        }
        public static byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }
        public static string GenerateRandomString(int length, string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        {
            var result = new StringBuilder(length);
            var bytes = GenerateRandomBytes(length * 4);
            for (int i = 0; i < length; i++)
            {
                var value = BitConverter.ToUInt32(bytes, i * 4);
                result.Append(charset[(int)(value % (uint)charset.Length)]);
            }
            return result.ToString();
        }
        #endregion

        #region File Encryption (AES-GCM)
        public static void EncryptFile(string inputFilePath, string outputFilePath, string key)
        {
            if (!File.Exists(inputFilePath)) throw new FileNotFoundException("Input file not found");
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
        public static void DecryptFile(string inputFilePath, string outputFilePath, string key)
        {
            if (!File.Exists(inputFilePath)) throw new FileNotFoundException("Input file not found");
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

        #region Private Helpers
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
    public class RSAKeyPair
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public int KeySize { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string ExportPublicKeyPem()
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(PublicKey), out _);
            return rsa.ExportRSAPublicKeyPem();
        }
        public string ExportPrivateKeyPem()
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(PrivateKey), out _);
            return rsa.ExportRSAPrivateKeyPem();
        }
    }
    public class EncryptionContext
    {
        public string Algorithm { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string KeyId { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
    #endregion
}