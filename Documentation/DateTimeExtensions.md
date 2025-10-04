# DateTimeExtensions

Utility methods for manipulating dates and times: rounding, range iteration, week calculations, safe parsing, and time zone adjustments.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| StartOfDay(this DateTime dt) | 00:00:00 of same day |
| EndOfDay(this DateTime dt) | 23:59:59.9999999 (precision aware) |
| StartOfWeek(this DateTime dt, DayOfWeek first = Monday) | First day boundary |
| EndOfWeek(this DateTime dt, DayOfWeek first = Monday) | Last day boundary |
| StartOfMonth / EndOfMonth | Month boundaries |
| Round(this DateTime dt, TimeSpan interval) | Round to nearest interval |
| Truncate(this DateTime dt, TimeSpan interval) | Floor to interval |
| EachDay(this DateTime from, DateTime to) | Enumerate days inclusive |
| ToUnixTimeSeconds(this DateTime dt) | Convert UTC to epoch seconds |
| FromUnixTimeSeconds(long seconds) | Construct UTC DateTime |

(Confirm actual list in code.)

---
## Examples
```csharp
var sod = now.StartOfDay();
foreach(var d in startDate.EachDay(endDate)) { /* ... */ }
var rounded = timestamp.Round(TimeSpan.FromMinutes(5));
```

---
## Notes
- Operations assume `DateTimeKind` correctness (prefer UTC internally).
- When rounding with daylight saving transitions consider using UTC to avoid ambiguity.

---
## Limitations
- Does not handle calendars other than Gregorian.
- `EndOfDay` precision may vary depending on ticks logic.
