# ObjectExtensions

Reflection and object manipulation helpers: shallow copy, property access, type conversion, null utilities, and dynamic merge operations.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| GetPropertyValue(this object obj, string name) | Fetch property via reflection (case-insensitive option) |
| SetPropertyValue(this object obj, string name, object? value) | Assign property if writable |
| CopyPropertiesTo(this object src, object dest) | Shallow copy matching properties |
| MergeObjects(this object target, params object[] sources) | Copy non-null source values |
| ToDictionary(this object obj) | Convert public properties to dictionary |
| IsNullOrDefault<T>(this T value) | Check default value semantic |

(Adjust per actual implementation.)

---
## Example
```csharp
var dict = entity.ToDictionary();
patch.MergeObjects(defaults);
```

---
## Guidance
- Consider caching `PropertyInfo` arrays for performance (likely already implemented).
- Avoid excessive reflection in hot loops; precompile delegates when possible.

---
## Limitations
- Deep copy not provided here (see DataMapperExtensions for deep clone).
- Indexer and non-public properties ignored.
