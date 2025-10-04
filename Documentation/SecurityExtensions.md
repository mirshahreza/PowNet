# SecurityExtensions

Validation and lightweight sanitization utilities aimed at quickly detecting common input risks (SQL Injection, XSS, Path Traversal, weak passwords, malformed email/URL/phone) plus a few defensive helpers. They supplement (not replace) parameterized queries, output encoding and CSP.

---
## Method Reference

### ValidateSqlSafety(this string? input, string? parameterName = null)
Heuristic detection of obvious SQL injection attempts (dangerous keywords, regex patterns, suspicious characters, encoded attacks). Returns `ValidationResult`.
```csharp
var sqlCheck = userText.ValidateSqlSafety("username");
if(!sqlCheck.IsValid) sqlCheck.ThrowIfInvalid();
```

### ValidateXssSafety(this string? input, string? parameterName = null)
Flags script blocks, inline event handlers, `javascript:` or dangerous `data:` URIs, encoded XSS patterns.
```csharp
var xss = comment.ValidateXssSafety("comment");
```

### ValidatePathSafety(this string? filePath, string? parameterName = null, bool allowAbsolutePaths = false)
Detects `../` traversal, absolute path usage (optional), dangerous extensions, null bytes, reserved device names.
```csharp
filePath.ValidatePathSafety("upload").ThrowIfInvalid();
```

### ValidateEmail(this string? email, string? parameterName = null)
Simplified RFC checks (format, length limits, no consecutive dots). Returns issues if invalid.
```csharp
var emailRes = email.ValidateEmail("email");
```

### ValidatePhoneNumber(this string? phone, string? parameterName = null, bool requireCountryCode = false)
Normalizes formatting; enforces length 7–15; optionally requires leading `+`.
```csharp
var phoneRes = phone.ValidatePhoneNumber("phone", requireCountryCode:true);
```

### ValidateUrl(this string? url, string? parameterName = null, string[]? allowedSchemes = null)
Ensures scheme allowed (default http/https), host present, disallows localhost/private ranges & suspicious patterns.
```csharp
var urlRes = link.ValidateUrl("website", new[]{"https"});
```

### ValidatePasswordStrength(this string? password, PasswordPolicy? policy = null)
Scores and classifies password (length, character classes, penalties for common / repeated / sequential patterns). Returns `PasswordValidationResult` (inherits `ValidationResult`).
```csharp
var pwRes = password.ValidatePasswordStrength(PasswordPolicy.Strict);
if(!pwRes.IsValid) Console.WriteLine(string.Join(";", pwRes.Issues));
```

### SanitizeForHtml(this string? input)
HTML?encodes entire string then strips script patterns & inline handlers (defense-in-depth; not a rich HTML sanitizer).
```csharp
string safeHtml = raw.SanitizeForHtml();
```

### SanitizeForSql(this string? input)
Escapes `'` and removes obvious injection punctuation (`-- ; /* */`). For final defense only—still use parameters.
```csharp
string fragment = userVal.SanitizeForSql();
```

### SanitizeFilePath(this string? filePath, bool allowDirectorySeparators = true)
Removes traversal markers, null bytes, unsafe characters; optionally strips separators.
```csharp
string stored = originalName.SanitizeFilePath(allowDirectorySeparators:false);
```

### ConstantTimeEquals(this ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
Timing?attack resistant byte comparison.
```csharp
bool match = expected.AsSpan().ConstantTimeEquals(provided.AsSpan());
```

### SafeJoin(this DirectoryInfo root, params string[] segments)
Builds canonical path & ensures it stays under root. Throws on traversal.
```csharp
var fullPath = new DirectoryInfo(rootPath).SafeJoin("sub","file.txt");
```

### EnsureNotDefault<T>(this T value, string paramName)
Throws `ArgumentException` if value equals default(T).
```csharp
int size = pageSize.EnsureNotDefault("pageSize");
```

---
## Supporting Types
### ValidationResult
| Property | Meaning |
|----------|---------|
| IsValid | True when no issues |
| ErrorMessage | Summary text when invalid |
| Issues | Detailed findings |

Methods: `Success()`, `Failure(msg, issues?)`, `ThrowIfInvalid()`.

### PasswordValidationResult : ValidationResult
Adds `PasswordStrength Strength`, `int Score`.

### PasswordPolicy
Configurable requirements & three presets:
- `Default` (min 8, recommended 12, all complexity)
- `Strict` (min 12, recommended 16, strict requirements)
- `Relaxed` (min 6, reduced complexity)

### PasswordStrength enum
`VeryWeak, Weak, Medium, Strong, VeryStrong`.

---
## Usage Pattern Example
```csharp
var validators = new[]{
    form.Email.ValidateEmail("email"),
    form.Website.ValidateUrl("website"),
    form.Password.ValidatePasswordStrength(),
    form.UploadName.ValidatePathSafety("upload")
};
var failures = validators.Where(v => !v.IsValid).ToList();
if (failures.Any())
{
    foreach (var f in failures)
        Console.WriteLine($"Invalid: {f.ErrorMessage} -> {string.Join(",", f.Issues)}");
    return; // reject
}
```

---
## Limitations & Notes
- Regex heuristics may produce false positives.
- Not a substitute for prepared statements / templated output.
- URL & email validation simplified (no IDN/punycode normalization).
- Password common list limited; integrate breach APIs for stronger checks.

---
## Change Highlights
- Consolidated helper methods & added defensive utilities (`SafeJoin`, `EnsureNotDefault`).
