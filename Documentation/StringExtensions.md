# StringExtensions & StringArrayExtensions

Common string helpers for trimming, null/empty handling, joining, splitting, normalization and safe comparisons.

---
## Representative Methods (String)
| Method | Purpose |
|--------|---------|
| NullIfEmpty(this string? s) | Return null when string empty/whitespace |
| DefaultIfNullOrWhiteSpace(this string? s, string fallback) | Fallback replacement |
| Truncate(this string s, int max, string ellipsis="…") | Safe length limit |
| ToSafeFileName(this string s, char replacement = '_') | Remove invalid path chars |
| EqualsIgnoreCase(this string a, string b) | Ordinal case-insensitive compare |
| IsNumeric(this string s) | Quick numeric check |
| RemoveDiacritics(this string s) | Strip accent marks |
| ToSlug(this string s) | Lowercase URL slug |

## Representative Methods (String Array)
| Method | Purpose |
|--------|---------|
| JoinSmart(this IEnumerable<string?> values, string separator = ",") | Ignore null/empty items |
| ToCsvEscaped(this IEnumerable<string?> values) | Quote & escape for CSV |

(Confirm actual implementation.)

---
## Examples
```csharp
string slug = title.ToSlug();
string? maybe = text.NullIfEmpty();
var line = items.JoinSmart("|");
```

---
## Guidance
- Prefer culture-invariant comparisons for identifiers.
- When producing user-visible text consider explicit culture operations.

---
## Extension Ideas
| Idea | Benefit |
|------|---------|
| Span-based parsing | Reduce allocations in hot paths |
| Pooled string builder | Efficient large concatenations |
