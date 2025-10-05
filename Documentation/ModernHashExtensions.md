# ModernHashExtensions

Self-describing PBKDF2-SHA256 password/secret hashing helpers.

Format produced:
```
PBKDF2-SHA256$<iterations>$<saltBase64>$<hashBase64>
```

## Methods
### GetHash(this string input, int iterations = 100_000, int saltSize = 32, int hashSize = 32)
Returns a formatted hash string containing algorithm id, iteration count, salt and hash (all Base64 except the iteration integer).

```csharp
string stored = password.GetHash();
// e.g. PBKDF2-SHA256$100000$BASE64SALT$BASE64HASH
```

### VerifyHash(this string input, string stored)
Parses stored format, recomputes PBKDF2-SHA256 using the embedded salt & iteration count, compares in constant time.

```csharp
bool ok = password.VerifyHash(stored);
```

### GetHashIterations(this string stored)
Extracts iteration count (nullable int) from a stored hash if valid.

```csharp
int? it = stored.GetHashIterations();
```

## Implementation Notes
- Uses `RandomNumberGenerator.GetBytes` for salt.
- Migrated to static API `Rfc2898DeriveBytes.Pbkdf2` (no deprecated constructors).
- Constant-time equality via `CryptographicOperations.FixedTimeEquals`.
- Default sizes: 32-byte salt, 32-byte derived key (256-bit) suitable for PBKDF2-SHA256.

## Security Guidance
- Increase iterations over time (track via `GetHashIterations`).
- Consider migration strategy to memory-hard KDF (Argon2 / scrypt) for future versions.
- Always store full self-describing string; avoid truncating salt or hash.

## Migration Strategy Example
When a user logs in:
1. Inspect existing stored hash format.
2. If iterations below current policy, recompute new hash and update.
3. Optionally migrate legacy (MD5 / MD4) by verifying old format then issuing PBKDF2 string.

Pseudo:
```csharp
if (legacyMd5 == ComputeLegacyMd5(input))
    stored = input.GetHash(newIter); // replace with modern format
else if (input.VerifyHash(stored) && stored.GetHashIterations() < policy.Iterations)
    stored = input.GetHash(policy.Iterations);
```

## Difference vs AuthExtensions HashPassword
`AuthExtensions.HashPassword` returns a structured object (`HashedPassword`) with separate fields. `ModernHashExtensions` produces a single portable string easier for storage / migration.

Use whichever storage model aligns with your persistence layer (object columns vs. single text column).
