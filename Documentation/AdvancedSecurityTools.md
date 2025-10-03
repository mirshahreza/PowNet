# AdvancedSecurityTools

Unified advanced security helper toolkit providing: secure password generation, entropy estimation, basic weak password detection, HTML / file name / CSV sanitization, secure random token creation (multiple formats), HOTP / TOTP one–time passwords, digital signatures (HMAC + RSA hook), and HTTP security header composition.

---
## Contents
1. Overview & Design Notes
2. Password Utilities
3. Sanitization Helpers
4. Token Generation (Random / HOTP / TOTP)
5. Digital Signatures
6. Security Headers
7. Supporting Types (TokenFormat, SignatureAlgorithm, SecurityHeadersConfig)
8. End?to?End Usage Examples

---
## 1. Overview & Design Notes
- All crypto randomness uses `RandomNumberGenerator` (CSPRNG).
- HOTP/TOTP implementation follows RFC 4226 / 6238 (SHA?1 variant) for compatibility.
- HTML sanitization performs a light defensive strip (NOT a full HTML sanitizer for untrusted rich content; for high?risk contexts integrate a battle?tested library like AngleSharp / HtmlSanitizer).
- Methods are static & side?effect free except for randomness.

Thread safety: All methods are stateless; per?call RNG instances are disposed immediately.

---
## 2. Password Utilities
### GenerateSecurePassword
```csharp
string pwd = AdvancedSecurityTools.GenerateSecurePassword(
    length: 20,
    includeUppercase: true,
    includeLowercase: true,
    includeNumbers: true,
    includeSpecialChars: true,
    excludeChars: "0O1lI|`",   // remove ambiguous glyphs
    avoidAmbiguous: true);
```
Generates a cryptographically secure random password ensuring at least one character from every selected class. If all character class flags are false an `ArgumentException` is thrown.

Parameters:
- length: Total password length.
- include* flags: Toggle individual character sets.
- excludeChars + avoidAmbiguous: Filter ambiguous characters (e.g. `O` vs `0`).

Return: Randomized password (characters shuffled after forced inclusions).

### CalculatePasswordEntropy
```csharp
double bits = AdvancedSecurityTools.CalculatePasswordEntropy(pwd);
```
Estimates entropy = `length * log2(charsetSize)` using detected character classes. Approximation; does not incorporate pattern penalties.

### IsPasswordCompromisedAsync
```csharp
bool weak = await AdvancedSecurityTools.IsPasswordCompromisedAsync("password123");
```
Demo weak password check against a small in?memory set (placeholder for HaveIBeenPwned style API integration). Returns true if matched (case?insensitive).

---
## 3. Sanitization Helpers
### SanitizeHtmlContent (extension on string)
```csharp
string raw = "<script>alert(1)</script><a href=\"javascript:alert(2)\">x</a>";
string safe = raw.SanitizeHtmlContent();
```
Removes:
- `<script>` blocks (+ contents)
- Inline event handlers `on*=`
- `javascript:` and `data:` URI patterns in `href`/`src`
- Tags not in `allowedTags` (default: formatting & anchor basics)

Note: Not a full DOM policy sanitizer.

### SanitizeFileName
```csharp
string safeFile = "inva?lid:na*me.txt".SanitizeFileName();
```
Replaces OS?invalid filename characters with `_`, collapses repeats, trims replacement chars, and produces a fallback random stub for reserved names (`CON`, `NUL`, ...).

### SanitizeCsvField
```csharp
string csvSafe = "=2+3".SanitizeCsvField();  // => "'=2+3"
```
Neutralizes CSV injection vectors by prefixing `'` if the field starts with `= + - @` or control chars, and RFC4180?quotes if containing comma / quote / newline.

---
## 4. Token Generation (Random / HOTP / TOTP)
### GenerateSecureToken
```csharp
string hex = AdvancedSecurityTools.GenerateSecureToken(16, TokenFormat.Hex);
string b64url = AdvancedSecurityTools.GenerateSecureToken(32, TokenFormat.Base64Url);
string alphaNum = AdvancedSecurityTools.GenerateSecureToken(24, TokenFormat.Alphanumeric);
```
Produces random bytes then encodes according to `TokenFormat`:
- Hex: lowercase hex string
- Base64: standard Base64 w/o padding
- Base64Url: URL safe variant (RFC 4648) w/o padding
- Alphanumeric: Index into `[A-Za-z0-9]`

### GenerateHOTP
```csharp
string hotp = AdvancedSecurityTools.GenerateHOTP(secret: "shared-key", counter: 1234, digits: 6);
```
Implements dynamic truncation of HMAC?SHA1 result. `digits` defines output length (mod 10^digits).

### GenerateTOTP
```csharp
string totp = AdvancedSecurityTools.GenerateTOTP(secret: "shared-key", digits: 6, stepSize: 30);
```
Computes HOTP with time counter = floor(unixTime / stepSize).

### VerifyTOTP
```csharp
bool valid = AdvancedSecurityTools.VerifyTOTP("shared-key", totp, windowSize: 1);
```
Sliding verification across ±`windowSize` time steps (each = 30s by default) to tolerate minor clock drift. Uses constant?time compare.

Security Note: For production, prefer a stronger hash (SHA?256) variant and enforce rate limiting.

---
## 5. Digital Signatures
### CreateDigitalSignature
```csharp
// HMAC (symmetric)
string sig = AdvancedSecurityTools.CreateDigitalSignature(
    data: "important-payload",
    privateKey: "hmac-secret",
    algorithm: SignatureAlgorithm.HMAC_SHA256);
```
Dispatches to:
- `data.ComputeHMAC(key)` (must exist elsewhere in project) for HMAC_SHA256
- `data.SignRSA(privateKey)` for RSA_SHA256

### VerifyDigitalSignature
```csharp
bool ok = AdvancedSecurityTools.VerifyDigitalSignature(
    data: "important-payload",
    signature: sig,
    key: "hmac-secret",
    algorithm: SignatureAlgorithm.HMAC_SHA256);
```
Returns false on signature mismatch OR any internal exception (fail?closed pattern).

RSA Note: `privateKey`/`key` strings assume existing helper conversions (e.g. PEM) in extension methods not shown here.

---
## 6. Security Headers
### GenerateSecurityHeaders
```csharp
var headers = AdvancedSecurityTools.GenerateSecurityHeaders(SecurityHeadersConfig.Strict);
foreach (var kv in headers)
{
    response.Headers[kv.Key] = kv.Value; // pseudo code
}
```
Generates hardened defaults:
- `Strict-Transport-Security` (if enabled)
- `Content-Security-Policy`
- `X-Frame-Options`
- `X-Content-Type-Options`
- `X-XSS-Protection` (legacy header – optionally remove if not desired)
- `Referrer-Policy`
- `Permissions-Policy`
- Clears server identification (`Server`, `X-Powered-By`).

Extend by modifying `SecurityHeadersConfig`.

---
## 7. Supporting Types
### TokenFormat
| Value | Meaning |
|-------|---------|
| Base64 | Standard Base64 (no trailing `=`) |
| Base64Url | URL safe variant |
| Hex | Lowercase hex encoding |
| Alphanumeric | Only `[A-Za-z0-9]` set |

### SignatureAlgorithm
| Value | Notes |
|-------|-------|
| RSA_SHA256 | Uses RSA + SHA256 (extension method dependency) |
| HMAC_SHA256 | Symmetric HMAC (SHA256) |

### SecurityHeadersConfig
Properties:
- EnableHSTS (default true)
- HSTSMaxAge (seconds; default 31536000 ~ 1 year)
- HSTSPreload (adds `; preload`)
- ContentSecurityPolicy (default `default-src 'self'`)
- XFrameOptions (default `DENY`)
- ReferrerPolicy (default `strict-origin-when-cross-origin`)
- PermissionsPolicy (sample restrictive defaults)

Factory presets:
```csharp
var def = SecurityHeadersConfig.Default;
var strict = SecurityHeadersConfig.Strict; // CSP: default-src 'none'; script/style/img 'self'
```

---
## 8. End?to?End Usage Examples
### Issue a Password + TOTP for 2FA Setup
```csharp
string onboardingPwd = AdvancedSecurityTools.GenerateSecurePassword(18);
string provisioningSecret = AdvancedSecurityTools.GenerateSecureToken(20, TokenFormat.Base64Url);
string initialTotp = AdvancedSecurityTools.GenerateTOTP(provisioningSecret);
```

### API Hardening (Minimal)
```csharp
var secHeaders = AdvancedSecurityTools.GenerateSecurityHeaders();
foreach (var h in secHeaders)
    httpContext.Response.Headers[h.Key] = h.Value;
```

### File Upload Normalization + Audit Token
```csharp
string clientName = formFile.FileName.SanitizeFileName();
string auditToken = AdvancedSecurityTools.GenerateSecureToken(24, TokenFormat.Hex);
// store (clientName, auditToken)
```

### CSV Export Cell Safety
```csharp
string safeCell = userInput.SanitizeCsvField();
writer.WriteLine(safeCell);
```

---
## Security Hardening Suggestions (Beyond Scope)
- Integrate PBKDF2 / Argon2 for password hashing (these helpers do NOT hash).
- Replace SHA1 HOTP with SHA256/512 variant if interoperable.
- Rate limit TOTP verification attempts.
- Consider full HTML sanitizer for user?generated rich content.

---
## Limitations
- Weak password detection list is illustrative only.
- RSA helpers rely on external extension methods not documented here.
- HTML sanitizer intentionally minimal.
