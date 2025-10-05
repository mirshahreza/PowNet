# LegacyHashingExtensions

MD5 / MD4 hashing retained ONLY for backward compatibility with pre-existing stored hashes.

> DO NOT use these algorithms for new password or credential storage. They are cryptographically broken and fast (susceptible to brute force & collisions).

## Methods
### HashMd5(string input)
Returns lowercase hex MD5 digest of UTF-8 bytes or empty string if input null/empty.

### HashMd4(string input)
Returns lowercase hex MD4 digest (custom managed implementation based on RFC 1320). Provided solely for legacy migration.

## Migration Guidance
1. On user authentication, verify legacy hash.
2. If matches, immediately re-hash using PBKDF2 (`ModernHashExtensions.GetHash` or `AuthExtensions.HashPassword`).
3. Replace stored legacy value with modern salted, iterated hash.

Pseudo:
```csharp
if (legacyStored == LegacyHashingExtensions.HashMd5(input))
    stored = input.GetHash(); // PBKDF2-SHA256
```

## Security Notes
- MD5 & MD4 vulnerable to collision and preimage attacks; treat any legacy hash as untrusted.
- Never mix legacy and modern hash formats without clear prefixing / segregation.
- Consider adding an application-level flag recording last upgrade date per credential.

## Removal Plan
Once all legacy hashes upgraded, remove this file & any references to ensure no accidental future use.
