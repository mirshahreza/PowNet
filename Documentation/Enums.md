# Enums

Enumeration types used across the framework to model discrete option sets (configuration states, cache levels, access checks, logging modes, etc.). Centralizing documentation helps keep meaning clear and assists consumers.

---
## Representative Enums (Illustrative)
| Enum | Example Members | Purpose |
|------|-----------------|---------|
| CacheLevel | None, PerUser, AllUsers | Cache scoping for API responses |
| CheckAccessLevel | None, Basic, Strict | Authorization rule evaluation depth |
| SignatureAlgorithm | RSA_SHA256, HMAC_SHA256 | Digital signature strategy |
| TokenFormat | Base64, Base64Url, Hex, Alphanumeric | Random token encoding |
| PasswordStrength | VeryWeak .. VeryStrong | Classified password assessment |
| LogLevel | Trace .. Critical | Logging severity |

(Align with actual enums defined in code.)

---
## Conventions
- PascalCase names
- Avoid `Unknown` unless required for external mapping
- Provide explicit underlying type only when size/interop matters

---
## Example
```csharp
if (config.CacheLevel == CacheLevel.PerUser)
{
    key = $"{key}_{currentUser}";
}
```

---
## Guidance
- Prefer flags enums only for true bitwise combinable concepts.
- Add XML doc comments where enums are part of public API surface.

---
## Extension Ideas
| Area | Idea |
|------|------|
| Display metadata | Use attributes for UI names / descriptions |
| Serialization | Custom converters for compact JSON (e.g., short codes) |
