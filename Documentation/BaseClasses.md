# BaseClasses

Foundational types (results, pagination, base entities, domain exceptions) used across the framework to establish consistent patterns.

---
## Representative Types
| Type | Purpose |
|------|---------|
| Result / Result<T> | Standard success + error aggregation wrapper |
| PaginatedResult<T> | Paged data container (Items, TotalCount, Page, PageSize) |
| BaseEntity | Common entity fields (Id, CreatedAt, UpdatedAt, etc.) |
| AuditableEntity | Extends BaseEntity with CreatedBy / UpdatedBy |
| DomainEvent (if present) | Marker for event dispatch patterns |

(See source for exact members.)

---
## Result Pattern
```csharp
var ok = Result.Success();
var fail = Result.Failure("Invalid state");
var data = Result<int>.Success(42);
if (!data.IsSuccess) Log(data.Errors);
```

---
## Pagination Pattern
```csharp
return PaginatedResult<UserDto>.Create(list, totalCount, page, pageSize);
```

---
## Guidance
- Keep base entities minimal; composition often preferable to deep inheritance.
- Avoid putting behavior that depends on infrastructure (db context, http) into entities.

---
## Extension Ideas
| Area | Enhancement |
|------|-------------|
| Result | Add implicit conversions for ergonomic returns |
| Pagination | Include `HasNext` / `HasPrevious` flags |
| Entity | Add concurrency token support |
