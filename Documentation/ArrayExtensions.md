# ArrayExtensions

Utility extension methods for working with raw arrays (focus on performance, batching and safety). Where possible prefer `CollectionExtensions` / `CollectionPerformanceAnalyzer` for richer scenarios.

---
## Overview
Provides helpers for:
- Fast cloning / copying
- Searching (index lookup on sorted or unsorted arrays)
- Batch processing
- Safe null / length guards

---
## Key Methods (Typical)
| Method | Purpose |
|--------|---------|
| FastCopy<T>(this T[] source) | Efficiently clone value-type arrays (falls back for reference types) |
| FastIndexOf<T>(this T[] array, T item, bool isSorted = false) | Binary search when sorted else linear scan |
| ProcessInBatches<T>(this T[] array, Action<T[]> processor, int batchSize = 1000) | Invoke delegate over fixed-size slices |
| IsNullOrEmpty<T>(this T[]? array) | Convenience null/length check |
| ForEach<T>(this T[] array, Action<T> action) | Lightweight loop abstraction (inlined) |

(Exact set depends on implementation in source code.)

---
## Usage Example
```csharp
int[] data = GetLargeIntArray();
foreach (var chunk in data.Chunk(500)) // if standard helper exists
{
    // process subset
}
int pos = data.FastIndexOf(42, isSorted:true);
```

---
## Notes
- Methods favor minimal allocations; avoid LINQ in hot paths.
- For extremely large arrays consider spanning (`Span<T>`) variants (future enhancement).

---
## Limitations
- No bounds checking beyond typical guard clauses.
- Specialized algorithms (e.g., SIMD search) not included.
