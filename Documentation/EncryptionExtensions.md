# EncryptionExtensions

Unified cryptographic helper collection covering:
- Modern symmetric authenticated encryption (AES-256-GCM)
- RSA key pair generation, encryption/decryption, signing/verification
- Hashing (SHA-256 / SHA-512) & HMAC-SHA256
- PBKDF2-based key derivation (static `Rfc2898DeriveBytes.Pbkdf2` API) + secure random utilities
- File-level AES-GCM encryption/decryption

> WARNING: Default PBKDF2 salt is static for convenience and MUST be replaced with unique random salt per secret in production. Persist salt & iterations alongside ciphertext.

---
## Method Reference

### EncryptAesGcm(this string plaintext, string key)
AES-256-GCM encryption (random 96-bit nonce, 128-bit tag). Output Base64 = nonce || ciphertext || tag.
```csharp
string cipher = "Sensitive".EncryptAesGcm("passphrase");
```

### DecryptAesGcm(this string ciphertext, string key)
Reverse of `EncryptAesGcm`; validates tag integrity.
```csharp
string plain = cipher.DecryptAesGcm("passphrase");
```

### GenerateRSAKeyPair(int keySize = 2048)
Creates new RSA keys (DER exported, Base64). Returns `RSAKeyPair`.
```csharp
var pair = EncryptionExtensions.GenerateRSAKeyPair(3072);
```

### EncryptRSA / DecryptRSA
Envelope encryption using OAEP SHA-256.
```csharp
string enc = message.EncryptRSA(pair.PublicKey);
string dec = enc.DecryptRSA(pair.PrivateKey);
```

### SignRSA / VerifyRSASignature
PKCS#1 v1.5 SHA-256 signature operations.
```csharp
string sig = payload.SignRSA(pair.PrivateKey);
bool ok = payload.VerifyRSASignature(sig, pair.PublicKey);
```

### ComputeSHA256 / ComputeSHA512
Base64 hash of UTF8 bytes.
```csharp
string h256 = data.ComputeSHA256();
```

### ComputeHMAC / VerifyHMAC
HMAC-SHA256 using derived key material.
```csharp
string mac = body.ComputeHMAC("hmac-secret");
bool valid = body.VerifyHMAC(mac, "hmac-secret");
```

### DeriveKey(password, length, salt?, iterations)
PBKDF2 SHA-256 (static .NET API). Provide unique salt per secret.
```csharp
byte[] keyMaterial = EncryptionExtensions.DeriveKey("pwd", 32, EncryptionExtensions.GenerateRandomBytes(16));
```

### GenerateRandomBytes / GenerateRandomString
Cryptographically secure random material.
```csharp
var token = EncryptionExtensions.GenerateRandomString(40);
```

### EncryptFile / DecryptFile
AES-GCM file encryption (nonce || ciphertext || tag).
```csharp
EncryptionExtensions.EncryptFile("plain.bin", "cipher.bin", "file-key");
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
Metadata container (Algorithm / KeyId / Metadata dictionary) for higher-level orchestration.

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
- Use AES-GCM exclusively for new symmetric encryption (legacy AES-CBC removed).
- Consider Argon2 or scrypt for password hashing (not in scope here).
- Monitor crypto API updates (future .NET versions may expose additional primitives).

---
## Error Handling
Methods throw `PowNetSecurityException` with contextual parameters (`ErrorType`, etc.) for structured logging.
