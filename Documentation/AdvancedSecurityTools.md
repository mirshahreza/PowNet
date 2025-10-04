# AdvancedSecurityTools

High-level consolidated security utility class providing:
- Secure password generation with character class guarantees
- Password entropy estimation and basic weak password detection
- HTML / File Name / CSV sanitization helpers
- Secure random token generation in multiple encodings
- HOTP / TOTP one?time password generation & verification (RFC 4226 / 6238 style SHA?1)
- Digital signature creation & verification (HMAC SHA-256 + RSA SHA-256)
- Security HTTP header composition (HSTS, CSP, etc.)

> NOTE: This class offers building blocks; production systems still need proper secret storage, rate limiting, and robust HTML sanitization for rich content.

---
## Method Reference
Each method lists purpose, parameters, return value and a usage example.

### GenerateSecurePassword(int length = 16, bool includeUppercase = true, bool includeLowercase = true, bool includeNumbers = true, bool includeSpecialChars = true, string excludeChars = "0O1lI|`", bool avoidAmbiguous = true)
Generates a cryptographically secure password ensuring at least one character from each selected category, then shuffles.

Parameters: obvious boolean inclusion flags; `excludeChars` + `avoidAmbiguous` filter visually confusing glyphs.
Returns: `string` password.

Example:
```csharp
string pwd = AdvancedSecurityTools.GenerateSecurePassword(length:20, includeSpecialChars:true);
```

### CalculatePasswordEntropy(string password)
Estimates entropy in bits using detected character set size * length.
Returns: `double` (bits).
```csharp
double bits = AdvancedSecurityTools.CalculatePasswordEntropy(pwd);
```

### IsPasswordCompromisedAsync(string password)
Demo weak list check (in-memory). Returns: `Task<bool>` true if on weak list.
```csharp
bool weak = await AdvancedSecurityTools.IsPasswordCompromisedAsync("password123");
```

### SanitizeHtmlContent(this string html, string[]? allowedTags = null)
Removes script tags, inline event handlers, dangerous protocols, & non?allowed tags (light heuristic sanitizer). Returns sanitized HTML.
```csharp
string safe = rawHtml.SanitizeHtmlContent();
```

### SanitizeFileName(this string fileName, string replacement = "_")
Normalizes OS-invalid characters, collapses duplicates, guards reserved device names.
```csharp
string safeName = "bad?name<>.txt".SanitizeFileName();
```

### SanitizeCsvField(this string field)
Neutralizes CSV injection by prefixing `'` if leading special char and RFC4180 quoting.
```csharp
string safeCell = "=2+3".SanitizeCsvField();
```

### GenerateSecureToken(int length = 32, TokenFormat format = TokenFormat.Base64)
Creates random bytes and encodes as Hex / Base64 / Base64Url / Alphanumeric.
```csharp
string token = AdvancedSecurityTools.GenerateSecureToken(24, TokenFormat.Base64Url);
```

### GenerateHOTP(string secret, long counter, int digits = 6)
Computes HOTP (HMAC-SHA1, dynamic truncation). Returns numeric code zero?padded.
```csharp
string hotp = AdvancedSecurityTools.GenerateHOTP("shared", 1234);
```

### GenerateTOTP(string secret, DateTime? timestamp = null, int digits = 6, int stepSize = 30)
Derives HOTP counter from Unix time / step; returns code.
```csharp
string totp = AdvancedSecurityTools.GenerateTOTP("shared-secret");
```

### VerifyTOTP(string secret, string code, DateTime? timestamp = null, int windowSize = 1)
Checks provided code across ±window steps (30s default) using constant-time compare. Returns `bool`.
```csharp
bool ok = AdvancedSecurityTools.VerifyTOTP("shared-secret", totp);
```

### CreateDigitalSignature(string data, string privateKey, SignatureAlgorithm algorithm = SignatureAlgorithm.RSA_SHA256)
Dispatches to RSA or HMAC signing helpers (project extension methods). Returns Base64 signature.
```csharp
string sig = AdvancedSecurityTools.CreateDigitalSignature("payload","hmac-key", SignatureAlgorithm.HMAC_SHA256);
```

### VerifyDigitalSignature(string data, string signature, string key, SignatureAlgorithm algorithm = SignatureAlgorithm.RSA_SHA256)
Verifies signature; returns false on failure or exception.
```csharp
bool valid = AdvancedSecurityTools.VerifyDigitalSignature("payload", sig, "hmac-key", SignatureAlgorithm.HMAC_SHA256);
```

### GenerateSecurityHeaders(SecurityHeadersConfig? config = null)
Composes recommended security headers removing server fingerprinting.
Returns: `Dictionary<string,string>`.
```csharp
var headers = AdvancedSecurityTools.GenerateSecurityHeaders(SecurityHeadersConfig.Strict);
foreach (var kv in headers) response.Headers[kv.Key] = kv.Value; // pseudo
```

---
## Supporting Types
### TokenFormat
`Base64, Base64Url, Hex, Alphanumeric` – controls encoding.

### SignatureAlgorithm
`RSA_SHA256` (asymmetric), `HMAC_SHA256` (symmetric).

### SecurityHeadersConfig
Properties: HSTS enable/max/preload, CSP, frame options, referrer policy, permissions policy. Presets: `Default`, `Strict`.

---
## Practical Scenarios
```csharp
// Issue onboarding credentials
string tempPwd = AdvancedSecurityTools.GenerateSecurePassword(18);
string totpSecret = AdvancedSecurityTools.GenerateSecureToken(20, TokenFormat.Base64Url);
string firstCode = AdvancedSecurityTools.GenerateTOTP(totpSecret);

// Sign & verify webhook payload
string body = JsonSerializer.Serialize(payload);
string mac = AdvancedSecurityTools.CreateDigitalSignature(body, hmacKey, SignatureAlgorithm.HMAC_SHA256);
bool accepted = AdvancedSecurityTools.VerifyDigitalSignature(body, mac, hmacKey, SignatureAlgorithm.HMAC_SHA256);
```

---
## Security Notes
- Weak password list is illustrative; integrate external breach API for stronger detection.
- For TOTP in production consider migrating to SHA-256 variant and enforce attempt rate limits.
- HTML sanitizer is minimal—use a full DOM policy sanitizer for rich user content.

---
## Limitations
- Entropy estimation does not penalize predictable patterns.
- RSA helpers rely on project extension methods (`SignRSA`, etc.) not documented here.
