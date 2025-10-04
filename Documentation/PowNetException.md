# PowNetException & Related Exceptions

Central custom exception hierarchy providing structured context (key/value parameters) and specialized domain/security/validation variants.

---
## PowNetException
Base type adding:
- Context parameter collection (fluent `AddParam(key, value)`) for enriched logs
- Standardized error code / message pattern (if implemented)

Example:
```csharp
throw new PowNetException("Failed to process")
    .AddParam("OrderId", orderId)
    .AddParam("User", userName);
```

---
## Derived Exceptions (Typical)
| Type | Purpose |
|------|---------|
| PowNetValidationException | Input or business rule validation failures |
| PowNetSecurityException | Security policy / cryptographic errors |
| PowNetConfigurationException | Invalid or missing configuration |

---
## Pattern
```csharp
if (!validator.IsValid)
    throw new PowNetValidationException("Invalid profile").AddParam("Errors", string.Join(';', validator.Errors));
```

---
## Guidance
- Avoid overusing broad exceptions—prefer specific derived types.
- Include only safe, non-sensitive values in parameters (no secrets, raw tokens, etc.).

---
## Extension Ideas
| Area | Idea |
|------|------|
| Serialization | Provide structured JSON output for API error responses |
| Correlation | Auto-add correlation/request ID param |
