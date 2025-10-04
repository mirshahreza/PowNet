# DataTableExtensions

Helpers for exporting `DataTable` to CSV and removing unwanted columns (including automatic removal of binary `byte[]` columns) to keep output lightweight.

---
## 1. Export
### ToCSV(this DataTable dt, string tempDirPath, List<string>? exceptColumns = null, Dictionary<string,string>? columnTitles = null)
Writes the DataTable to a uniquely named CSV file inside `tempDirPath`.

Behavior:
- Generates random GUID-based filename (no hyphens) with `.csv` extension.
- Removes columns whose type is `byte[]` (binary) via `RemoveByteArrayColumns()`.
- Removes explicitly listed columns in `exceptColumns` via `RemoveColumns()`.
- Optional header renaming: if `columnTitles` contains a mapping for a column name, its header is replaced (commas stripped and newlines removed).
- Values containing commas are quoted; embedded newlines removed via `Replace(StringExtensions.NL, "")`.

Returns: void (writes file). If you need the generated file path, consider modifying implementation to return `fp`.

Example:
```csharp
var table = new DataTable();
table.Columns.Add("Id", typeof(int));
table.Columns.Add("Name", typeof(string));
table.Rows.Add(1, "Alice");
table.Rows.Add(2, "Bob, Jr");

table.ToCSV(Path.GetTempPath(), exceptColumns: new(){"InternalFlag"}, columnTitles: new(){ ["Name"] = "Full Name" });
```
File path pattern: `{tempDirPath}/{RandomGuidNoHyphens}.csv`.

Edge Cases:
- If directory does not exist you'll get an IOException (create directory beforehand).
- Empty table still writes header row (if columns exist) + newline.

---
## 2. Column Removal
### RemoveColumns(this DataTable dt, List<string>? exceptColumns = null)
Removes columns listed in `exceptColumns` if they exist.
```csharp
table.RemoveColumns(new(){"Secret"});
```

### RemoveByteArrayColumns(this DataTable dt)
Scans columns and removes those whose `DataType == typeof(byte[])`.
```csharp
table.RemoveByteArrayColumns();
```
Optimization: collects names first into a local list to avoid modifying collection while iterating.

---
## 3. Suggested Enhancements
| Need | Suggestion |
|------|------------|
| Return generated CSV path | Change `void` ? `string` and return `fp` |
| Large data performance | Stream rows with `IDataReader` or use `StringBuilder` buffer reuse |
| Value escaping | Expand to handle quotes (prefix double quotes by doubling) |
| Culture-specific separators | Parameterize delimiter (default `,`) |

---
## 4. Usage Pattern
```csharp
var headers = new Dictionary<string,string>{{"Name","DisplayName"}};
myDataTable.ToCSV(Path.Combine(env.ContentRootPath, "exports"), columnTitles: headers);
```

---
## 5. Limitations
- No handling for embedded double quotes inside values (improve by escaping `"` ? `""`).
- No progress reporting for very large tables.
- Current newline stripping may merge multi-line text fields inadvertently.

---
*Manual documentation.*
