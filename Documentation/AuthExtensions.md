# AuthExtensions

Authentication / authorization helper extensions simplifying role, claim and token handling plus structured password hashing.

---
## Overview
Includes helpers to:
- Hash & verify passwords (PBKDF2-SHA256) via `HashPassword` / `VerifyPassword` (returns `HashedPassword` object)
- Extract typed claim values (id, email, roles)
- Check role / permission membership
- Generate / validate JWT tokens (HS256)
- Generate secure API keys & verify via SHA-256
- Lightweight rate limiting primitives (in-memory)

For single self-describing password hash string instead of structured object see: `ModernHashExtensions`.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| HashPassword(string pwd, int iterations) | Returns `HashedPassword { Hash,Salt,Iterations,Algorithm }` |
| VerifyPassword(string pwd, HashedPassword stored) | Constant-time PBKDF2 verify |
| GenerateJwtToken(this ClaimsPrincipal, TimeSpan? exp, string? aud) | Issue HS256 token |
| ValidateJwtToken(this string token, string? aud) | Validate & return principal or null |
| HasRole / HasAnyRole / HasAllRoles | Role membership checks |
| GetUserId / GetUserName / GetEmail | Common claim extraction |
| GenerateApiKey(int length) | Secure random API key |
| HashApiKey / VerifyApiKey | SHA-256 base64 hashing & constant-time verify |

---
## Password Hashing Example
```csharp
var hp = password.HashPassword(iterations:150_000);
if (!input.VerifyPassword(hp)) return Unauthorized();
```

`HashedPassword.Algorithm` currently fixed to `PBKDF2-SHA256`.

---
## JWT Example
```csharp
string token = User.GenerateJwtToken(TimeSpan.FromHours(12));
var principal = token.ValidateJwtToken();
if (principal == null) return Unauthorized();
```

---
## API Key Example
```csharp
string apiKey = AuthExtensions.GenerateApiKey(40);
string storedHash = apiKey.HashApiKey();
bool ok = provided.HashApiKey() == storedHash; // or VerifyApiKey helper
```

---
## Security Guidance
- Rotate JWT signing secret (`PowNetConfiguration.EncryptionSecret`) periodically.
- Consider sliding expiration or refresh token pattern for long-lived sessions.
- Increase PBKDF2 iterations over time; rehash on successful login if below policy.
- Store API keys only as hashes (never plaintext post-creation).

---
## Comparison: Structured vs String Hash
| Aspect | AuthExtensions.HashPassword | ModernHashExtensions.GetHash |
|--------|-----------------------------|------------------------------|
| Storage | Multi-field object | Single string format |
| Migration ease | Higher (metadata fields) | Simpler (parse segments) |
| Extensibility | Add fields (CreatedAt, etc.) | Must keep format stable |

Use the one best fitting persistence schema.

---
## Limitations
- In-memory rate limiting not distributed (no eviction strategy beyond process lifetime).
- JWT implementation assumes symmetric key HS256 only.
- No refresh token or scope/permission model built-in.
