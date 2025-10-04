# UserModels

User-centric domain models (entities and DTOs) plus related value objects (roles, permissions, claims, profile data).

---
## Representative Types
| Type | Purpose |
|------|---------|
| UserServerObject | Runtime user context (Id, UserName, Roles, AllowedActions, Data) |
| UserDto / UserSummaryDto | Transfer objects for API responses |
| Role / Permission models | Authorization domain primitives |
| UserProfile | Extended profile information |

(Verify actual types in code.)

---
## Example Usage
```csharp
var user = new UserServerObject
{
    Id = 5,
    UserName = "alice",
    Roles = new(),
    AllowedActions = new(),
    Data = new()
};
```

---
## Guidance
- Keep API DTOs separate from persistence entities to avoid unintended coupling.
- Sanitize user-provided profile fields before storage (XSS mitigation).

---
## Extension Ideas
| Area | Idea |
|------|------|
| Auditing | Track last login / password change timestamps |
| Preferences | Strongly typed settings object |
