# PowNetClassGenerator

Utility for programmatically generating class source code (static utility classes or data containers) with customizable using directives and templated method bodies.

---
## Core Types
| Type | Purpose |
|------|---------|
| PowNetClassGenerator | Builder for a single static class (methods aggregated from provided lists) |
| PowNetMethodGenerator | Helper to produce a single method implementation based on `MethodTemplate` |
| MethodTemplate (enum) | Template selector: `NotMapped, DbProducer, DbScalarFunction, DbTableFunction, DbDialog` |

---
## PowNetClassGenerator Usage
```csharp
var gen = new PowNetClassGenerator("UserProcedures", "MyApp.Data")
    .AddUsing("using System.Text.Json")
    .AddUsing("using MyApp.Infrastructure");

// Add dialog methods (names only; body rendered by template)
gen.DbDialogMethods.AddRange(new[]{ "FindUsers", "GetUserDetails" });

// Add scalar function wrapper with arguments ("<type> <name>")
gen.DbScalarFunctionMethods["GetUserCount"] = new(){ "int userId" };

string source = gen.ToCode();
File.WriteAllText("UserProcedures.g.cs", source);
```

---
## Custom Usings
The generator exposes a `Usings` collection and an `AddUsing` helper. Only minimal default (`using System;`) is included automatically.
```csharp
gen.AddUsing("using System.Linq");
// or direct
gen.Usings.Add("using System.Collections.Generic;");
```
The final emitted block is the ordered distinct set of all directives.

---
## Method Templates
| Template | Generated Signature (Conceptual) | Body Placeholder |
|----------|----------------------------------|------------------|
| DbDialog | `object? Name(JsonElement clientQuery, PowNetUser? actor)` | Comment placeholder |
| NotMapped | `object? Name(PowNetUser? actor)` | Returns `true` |
| DbProducer | `object? Name(string dbConfigName, ...args)` | Placeholder for stored proc call |
| DbScalarFunction | `object? Name(string dbConfigName, ...args)` | Placeholder for scalar fn call |
| DbTableFunction | `object? Name(string dbConfigName, ...args)` | Placeholder for table fn call |

Arguments list is provided as `List<string>` with entries formatted like `"int userId"`. They are concatenated after an initial `string dbConfigName` parameter (added automatically when at least one template requires a DB config name).

---
## Extending Templates
To add new templates:
1. Extend `MethodTemplate` enum.
2. Add a corresponding branch in `PowNetMethodGenerator`.
3. Implement a new template method in `CSharpTemplates` similar to existing ones.

---
## Generated Output Structure
```csharp
$Usings$

namespace <Namespace>
{
    public static class <ClassName>
    {
        // Repeated method bodies
    }
}
```
`$Usings$` is replaced by the aggregated directives (one per line).

---
## Validation & Safety Notes
- Generator does not validate that method names are unique; ensure no duplicates before calling `ToCode()`.
- No SQL or database logic is emitted—placeholders intentionally keep generated code side-effect free.
- If you include `JsonElement` parameters add the `System.Text.Json` using yourself.

---
## Suggested Enhancements
| Area | Idea |
|------|------|
| Naming | Add automatic PascalCase normalization for method names |
| Code Style | Integrate formatting / indent normalization post generation |
| Annotations | Allow attribute decoration per method (e.g., `[Obsolete]`) |
| Async Support | Provide async template variants returning `Task<object?>` |
| Partial Classes | Option to emit `partial` keyword for manual augmentation |

---
## Minimal Example
```csharp
var gen = new PowNetClassGenerator("HealthChecks", "MyApp.Runtime")
    .AddUsing("using System.Text.Json");
gen.NotMappedMethods.Add("Ping");
var src = gen.ToCode();
```
Produces something like:
```csharp
using System;
using System.Text.Json;

namespace MyApp.Runtime
{
    public static class HealthChecks
    {
        public static object? Ping(PowNetUser? actor)
        {
            return true;
        }
    }
}
```

---
*Updated to reflect configurable using directives and current template behavior.*
