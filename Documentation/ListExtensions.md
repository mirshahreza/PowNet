# ListExtensions

List-specific helpers that augment standard `List<T>` functionality (complementing generic collection utilities in `CollectionExtensions`).

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| AddRange<T>(this List<T> list, params T[] items) | Append items if not null/empty |
| RemoveWhere<T>(this List<T> list, Func<T,bool> predicate) | In-place conditional removal |
| FastContains<T>(this List<T> list, T item) | Small-list optimized contains |
| Partition<T>(this List<T> list, int size) | Yield fixed-size chunks |
| ProcessParallel<T>(this List<T> list, Action<T> action, int? maxDop=null) | Parallel processing wrapper |

---
## Example
```csharp
foreach (var batch in items.Partition(250))
    Persist(batch);

int trimmed = users.RemoveWhere(u => u.IsInactive);
```

---
## Notes
- Reverse iteration removal avoids index shift issues.
- Parallel processing should be reserved for CPU-bound work; for I/O prefer async batching.

---
## Limitations
- No cancellation token support in parallel helper (could be added).
