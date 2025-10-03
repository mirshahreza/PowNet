# SecurityExtensions

Comprehensive input validation and lightweight sanitization helpers. These methods are designed to quickly assess untrusted input for common classes of vulnerabilities: SQL Injection, XSS, Path Traversal, URL misuse, weak / malformed credentials, and unsafe file system usage. They return rich `ValidationResult` objects (never throw unless you explicitly call `ThrowIfInvalid()`).

> NOTE: These are defensive, pattern?based heuristics – not a replacement for parameterized SQL, templated HTML rendering, or full content security policies. Use them as an additional guardrail layer.

---
## Contents
1. Core Validation Methods
2. Password Strength Evaluation
3. Sanitization Utilities
4. Low?Level Security Helpers
5. Supporting Types (`ValidationResult`, `PasswordPolicy`, etc.)
6. Usage Patterns & Best Practices
7. Extended Examples

---
## 1. Core Validation Methods
Each validator inspects the provided string and accumulates issue messages. A clean result has `IsValid == true` and an empty `Issues` list.

### ValidateSqlSafety(string? input, string? parameterName = null)
Detects obvious SQL injection indicators.
- Scans for dangerous keywords (e.g. `DROP`, `UNION`, `EXEC`)
- Regex patterns (e.g. `' OR 1=1`, comment abuse, stacked statements)
- Suspicious characters (`' ; -- /* */ = < > | &`)
- Encoded payload attempts (URL decoded re?scan)

Example:
```csharp
var vr = userInput.ValidateSqlSafety("username");
if(!vr.IsValid) vr.ThrowIfInvalid(); // or log vr.Issues
```

### ValidateXssSafety(string? input, string? parameterName = null)
Looks for typical XSS vectors:
- `<script>` blocks
- Inline event handlers (`onclick=`, `onerror=` ...)
- `javascript:` pseudo?protocol
- `data:` payload with HTML/script
- Encoded XSS (double decoding detection)

Example:
```csharp
var htmlResult = comment.ValidateXssSafety("comment");
if(!htmlResult.IsValid) Console.WriteLine(string.Join(";", htmlResult.Issues));
```

### ValidatePathSafety(string? filePath, string? parameterName = null, bool allowAbsolutePaths = false)
Checks:
- Relative traversal (`../` or `..\\`)
- Absolute path prohibition (unless allowed)
- Executable / scripting file extensions
- Null byte inclusion
- Windows reserved device names (e.g. `CON`, `NUL`)

Example:
```csharp
filePath.ValidatePathSafety("uploadPath").ThrowIfInvalid();
```

### ValidateEmail(string? email, string? parameterName = null)
RFC?inspired checks (simplified):
- Basic regex format
- Overall length <= 254
- Local part <= 64; domain <= 255
- No consecutive dots

Example:
```csharp
var emailRes = email.ValidateEmail("email");
if(!emailRes.IsValid) return BadRequest(emailRes.Issues);
```

### ValidatePhoneNumber(string? phoneNumber, string? parameterName = null, bool requireCountryCode = false)
Normalizes by removing common formatting symbols then validates:
- Regex pattern (digits + optional formatting) length 7–15
- Country code presence when required (leading '+')

Example:
```csharp
var phoneRes = phone.ValidatePhoneNumber("phone", requireCountryCode:true);
```

### ValidateUrl(string? url, string? parameterName = null, string[]? allowedSchemes = null)
Examines:
- Scheme membership (default `http|https`)
- Host presence
- Disallows localhost / private IP blocks (RFC1918) by default
- Suspicious patterns (data:, javascript:, file:, UNC, `../`)

Example:
```csharp
var urlRes = url.ValidateUrl("redirect", new[]{"https"});
```

---
## 2. Password Strength Evaluation
### ValidatePasswordStrength(string? password, PasswordPolicy? policy = null)
Scores password and classifies into: VeryWeak, Weak, Medium, Strong, VeryStrong.

Scoring dimensions:
- Length (minimum + recommended thresholds)
- Presence of uppercase / lowercase / digits / special characters
- Penalizes: common password list, long repeats (>3), sequential slices (e.g. `abc`, `123`, `qwe`)

Example:
```csharp
var policy = PasswordPolicy.Strict; // 12+ length etc.
var pwResult = password.ValidatePasswordStrength(policy);
if(!pwResult.IsValid)
    Console.WriteLine(string.Join(";", pwResult.Issues));
Console.WriteLine($"Score={pwResult.Score} Strength={pwResult.Strength}");
```

Policy presets:
- `PasswordPolicy.Default` (8 / 12)
- `PasswordPolicy.Strict`  (12 / 16 mandatory complexity)
- `PasswordPolicy.Relaxed` (6 / 8 fewer requirements)

---
## 3. Sanitization Utilities
These modify content to neutralize harmful constructs. Use AFTER validation (not a silver bullet).

### SanitizeForHtml(string? input)
- HTML encodes entire string (prevents raw tag injection)
- Strips: `<script>`, inline event handlers, `javascript:` pseudo protocol

Example:
```csharp
string safe = rawInput.SanitizeForHtml();
```

### SanitizeForSql(string? input)
Performs minimal neutralization (not a replacement for parameterized queries):
- Escapes single quotes ? `''`
- Removes `--` comments, `;` terminators, and block comment markers `/* */`

Example:
```csharp
var filtered = userValue.SanitizeForSql();
```

### SanitizeFilePath(string? filePath, bool allowDirectorySeparators = true)
Removes traversal fragments, null bytes, and illegal filesystem characters. Optionally strips directory separators for pure basenames.

Example:
```csharp
string storedName = uploadedName.SanitizeFilePath(allowDirectorySeparators:false);
```

---
## 4. Low?Level Security Helpers
### ConstantTimeEquals(this ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
Performs XOR aggregation to avoid early exit timing side?channels. Returns false if length mismatch.

Example:
```csharp
bool match = expected.AsSpan().ConstantTimeEquals(actual);
```

### SafeJoin(this DirectoryInfo root, params string[] segments)
Builds a canonical absolute path and ensures it remains inside the `root` directory boundary. Throws `UnauthorizedAccessException` on traversal.

Example:
```csharp
var root = new DirectoryInfo(basePath);
string full = root.SafeJoin("sub", "file.txt");
```

### EnsureNotDefault<T>(this T value, string paramName)
Utility guard that throws for default(T) (e.g. 0 for int, null for ref types, Guid.Empty, etc.).

Example:
```csharp
int pageSize = inputSize.EnsureNotDefault("pageSize");
```

---
## 5. Supporting Types
### ValidationResult
| Property | Meaning |
|----------|---------|
| IsValid | true if no issues recorded |
| ErrorMessage | Summary (when invalid) |
| Issues | Detailed list of findings |

Factory Methods:
```csharp
ValidationResult.Success();
ValidationResult.Failure("reason", new List<string>{"detail1","detail2"});
```
`ThrowIfInvalid()` raises `PowNetValidationException` with issue aggregation.

### PasswordValidationResult : ValidationResult
Adds:
- `PasswordStrength Strength`
- `int Score`

### PasswordPolicy
| Property | Purpose |
|----------|---------|
| MinLength | Minimum allowed length |
| RecommendedLength | Additional scoring threshold |
| RequireUppercase / Lowercase / Digits / SpecialChars | Complexity flags |
| SpecialCharacters | Set of recognized special glyphs |

Presets: `Default`, `Strict`, `Relaxed`.

### PasswordStrength (enum)
VeryWeak = 0, Weak, Medium, Strong, VeryStrong.

---
## 6. Usage Patterns & Best Practices
- ALWAYS still use parameterized queries; `ValidateSqlSafety` catches only blatant injection patterns.
- Prefer server?side templating or safe component frameworks; `ValidateXssSafety` is heuristic.
- Store only sanitized / normalized filenames; keep original if needed for display in a separate safe field.
- Combine password strength evaluation with breach checking (e.g. HIBP API) – current common list is illustrative.
- Treat any `Issues` content as security telemetry (log centrally with rate limiting to avoid log flooding).

---
## 7. Extended Examples
### Multi?Field Form Validation
```csharp
var emailV  = form.Email.ValidateEmail("email");
var urlV    = form.Website.ValidateUrl("website");
var pathV   = form.FilePath.ValidatePathSafety("filePath");
var passV   = form.Password.ValidatePasswordStrength();

var failures = new[]{emailV, urlV, pathV, passV}.Where(v => !v.IsValid).ToList();
if(failures.Any())
{
    foreach(var f in failures)
        Console.WriteLine($"Invalid: {f.ErrorMessage} -> {string.Join(",", f.Issues)}");
    return; // reject
}
```

### Safe Storage of Upload Filename
```csharp
string original = formFile.FileName;
string safeName = original.SanitizeFilePath(allowDirectorySeparators:false);
var targetPath = new DirectoryInfo(uploadRoot).SafeJoin(safeName);
await using var fs = File.Create(targetPath);
await formFile.CopyToAsync(fs);
```

### Enforcing Strict Password Policy at Registration
```csharp
var policy = PasswordPolicy.Strict;
var pwRes  = password.ValidatePasswordStrength(policy);
if(!pwRes.IsValid || pwRes.Strength < PasswordStrength.Strong)
    return Results.BadRequest(pwRes.Issues);
```

### Constant Time Session Token Comparison
```csharp
if(!expectedToken.AsSpan().ConstantTimeEquals(providedToken.AsSpan()))
    return Results.Unauthorized();
```

---
## Limitations
- Regex based; may produce false positives for unusual but benign input.
- No canonicalization of internationalized domain names (IDN) in URL/email validation.
- Not a replacement for layered security controls (CSP, output encoding libraries, WAF, RBAC).

---
## Change Log Notes
- Consolidated multiple safety checks into single extension class.
- Added `EnsureNotDefault` & `SafeJoin` for general defensive coding.
