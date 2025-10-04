# EncryptionExtensions

Unified cryptographic helper collection covering:
- Modern symmetric authenticated encryption (AES-256-GCM)
- Legacy AES-CBC compatibility (Encode/Decode)
- RSA key pair generation, encryption/decryption, signing/verification
- Hashing (SHA-256 / SHA-512) & HMAC-SHA256
- PBKDF2-based key derivation + secure random utilities
- File-level AES-GCM encryption/decryption

> WARNING: Default PBKDF2 salt is static for convenience and MUST be replaced with unique random salt per secret in production. Persist salt & iterations alongside ciphertext.

---
## Method Reference

### EncryptAesGcm(this string plaintext, string key)
Performs AES-256-GCM encryption (random 96-bit nonce, 128-bit tag). Output Base64 = nonce || ciphertext || tag.
```csharp
string cipher = "Sensitive".EncryptAesGcm("passphrase");
```
Throws `PowNetSecurityException` on invalid input or failure.

### DecryptAesGcm(this string ciphertext, string key)
Reverses `EncryptAesGcm`; validates tag integrity.
```csharp
string plain = cipher.DecryptAesGcm("passphrase");
```

### Encode(this string plaintext, string key)
Legacy AES-CBC (PKCS7) with random IV (Base64 output = IV || ciphertext).
```csharp
string legacy = "Hello".Encode("key123");
```

### Decode(this string ciphertext, string key)
Decrypts legacy AES-CBC payload.
```csharp
string again = legacy.Decode("key123");
```

### GenerateRSAKeyPair(int keySize = 2048)
Creates new RSA keys (raw DER bytes exported Base64). Returns `RSAKeyPair`.
```csharp
var pair = EncryptionExtensions.GenerateRSAKeyPair(3072);
```

### EncryptRSA(this string plaintext, string publicKey)
Encrypts using OAEP SHA-256.
```csharp
string enc = "secret".EncryptRSA(pair.PublicKey);
```

### DecryptRSA(this string ciphertext, string privateKey)
Decrypts OAEP encrypted data.
```csharp
string dec = enc.DecryptRSA(pair.PrivateKey);
```

### SignRSA(this string data, string privateKey)
Produces Base64 PKCS#1 v1.5 SHA-256 signature.
```csharp
string sig = "payload".SignRSA(pair.PrivateKey);
```

### VerifyRSASignature(this string data, this string signature, string publicKey)
Returns true if signature valid (false on any exception).
```csharp
bool ok = "payload".VerifyRSASignature(sig, pair.PublicKey);
```

### ComputeSHA256(this string input) / ComputeSHA512(this string input)
Returns Base64 hash of UTF8 bytes.
```csharp
string h256 = "data".ComputeSHA256();
```

### ComputeHMAC(this string input, string key)
HMAC-SHA256 using derived key (PBKDF2 output 32 bytes).
```csharp
string mac = body.ComputeHMAC("hmac-secret");
```

### VerifyHMAC(this string input, this string signature, string key)
Constant-time comparison of computed vs supplied HMAC.
```csharp
bool valid = body.VerifyHMAC(mac, "hmac-secret");
```

### DeriveKey(string password, int keyLength, byte[]? salt = null, int iterations = 100000)
PBKDF2 (SHA-256). **Use unique salt per password/secret in production**.
```csharp
byte[] keyMaterial = EncryptionExtensions.DeriveKey("pwd", 32, EncryptionExtensions.GenerateRandomBytes(16));
```

### GenerateRandomBytes(int length)
Cryptographically secure random bytes.
```csharp
var nonce = EncryptionExtensions.GenerateRandomBytes(12);
```

### GenerateRandomString(int length, string charset = "A..Za..z0..9")
Generates random characters (modulo bias acceptable for typical usage).
```csharp
string token = EncryptionExtensions.GenerateRandomString(40);
```

### EncryptFile(string inputFilePath, string outputFilePath, string key)
AES-GCM file encryption (writes nonce || ciphertext || tag).
```csharp
EncryptionExtensions.EncryptFile("plain.bin", "cipher.bin", "file-key");
```

### DecryptFile(string inputFilePath, string outputFilePath, string key)
File decryption counterpart.
```csharp
EncryptionExtensions.DecryptFile("cipher.bin", "plain.out", "file-key");
```

---
## Supporting Types
### RSAKeyPair
| Property | Description |
|----------|-------------|
| PublicKey | Base64 exported RSA public key (DER) |
| PrivateKey | Base64 exported RSA private key (DER) |
| KeySize | Key size in bits |
| CreatedAt | UTC creation timestamp |

Methods:
```csharp
string pubPem = pair.ExportPublicKeyPem();
string privPem = pair.ExportPrivateKeyPem();
```

### EncryptionContext
Metadata container (Algorithm / KeyId / Metadata dictionary) for higher-level orchestration (not used internally here).

---
## Usage Scenarios
```csharp
// Secure config blob
string cipher = configJson.EncryptAesGcm(masterKey);
string plain  = cipher.DecryptAesGcm(masterKey);

// API webhook signature
string mac = body.ComputeHMAC(sharedSecret);
if(!body.VerifyHMAC(mac, sharedSecret)) return; // reject

// RSA envelope
var keys = EncryptionExtensions.GenerateRSAKeyPair();
string encMsg = message.EncryptRSA(keys.PublicKey);
string decMsg = encMsg.DecryptRSA(keys.PrivateKey);
```

---
## Security Guidance
- Replace static salt with random per-secret salt & store alongside ciphertext.
- Prefer AES-GCM for new encryption; limit AES-CBC to legacy data migration.
- Consider Argon2 or scrypt for password hashing (not in scope here).
- Monitor crypto API obsolescence (AesGcm constructors with explicit tag size recommended in new .NET versions).

---
## Error Handling
All critical failures throw `PowNetSecurityException` with context parameters (`ErrorType`, etc.) enabling structured logging.
