# AuthExtensions

Authentication / authorization helper extensions simplifying role, claim and token handling.

---
## Overview
Includes helpers to:
- Extract typed claim values (id, email, roles)
- Check role / permission membership
- Generate / validate simple tokens (where applicable)
- Merge principal claims into custom user context objects

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| GetUserId(this ClaimsPrincipal principal) | Return user identifier (int/Guid parse) |
| GetUserName(this ClaimsPrincipal principal) | Return preferred name / fallback identity name |
| HasRole(this ClaimsPrincipal principal, string role) | Case-insensitive role check |
| HasAnyRole(this ClaimsPrincipal principal, params string[] roles) | Any-of role check |
| GetClaim(this ClaimsPrincipal principal, string type) | Raw claim value retrieval |
| ToUserContext(this ClaimsPrincipal principal) | Map to domain user object |

(Refer to code for exact signatures.)

---
## Usage Example
```csharp
if (!User.HasRole("Admin"))
    return Forbid();

var userId = User.GetUserId();
var ctx = User.ToUserContext();
```

---
## Notes
- Robust parsing: methods should gracefully return defaults instead of throwing.
- Avoid overloading JWT with large custom payloads—prefer data lookup post-auth.

---
## Security Guidance
- Never trust role / claim content without signature validation of the token source.
- Enforce defense-in-depth by verifying authorization at controller + service layers.

---
## Limitations
- Helpers assume standard claim type names; customize mapping for other identity providers.
