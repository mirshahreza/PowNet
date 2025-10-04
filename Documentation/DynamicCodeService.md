# DynamicCodeService

Service for compiling and loading C# code at runtime, enabling plug-in style extensions, dynamic rule evaluation, or script-like behaviors.

---
## Capabilities
| Feature | Description |
|---------|-------------|
| CompileSnippet | Compile a string of C# code into an in-memory assembly |
| CompileFiles | Compile one or more source files |
| LoadAssembly | Load previously compiled assembly bytes |
| ExecuteEntryPoint | Run known method (e.g., `Run`, `Execute`) by reflection |
| CreateInstance<T> | Instantiate type from dynamic assembly |

(Adapt to actual API.)

---
## Example (Conceptual)
```csharp
string src = @"public static class Greeter { public static string Say() => \"Hello\"; }";
var asm = DynamicCodeService.CompileSnippet(src);
var type = asm.GetType("Greeter")!;
string msg = (string)type.GetMethod("Say")!.Invoke(null, null)!;
```

---
## Security Considerations
- Never compile untrusted code without sandboxing (AppDomain isolation not available in .NET Core; consider WASM/containers).
- Enforce allowlist of referenced assemblies.
- Limit memory/time via cancellation and `AssemblyLoadContext` unloading.

---
## Guidance
- Cache compiled assemblies keyed by hash of source to avoid redundant compilation.
- Provide diagnostics (errors, warnings) surfaced to caller.

---
## Extension Ideas
| Area | Idea |
|------|------|
| Roslyn analyzers | Apply custom analyzers during compilation |
| Script host | Support `#r` and `using` directives similar to C# scripting |
| Hot reload | Replace existing dynamic types safely |

---
## Limitations
- In-memory assemblies cannot be unloaded prior to .NET 5+ `AssemblyLoadContext` usage; ensure custom context employed for unload.
- Reflection invocation slower than direct calls—consider delegate emission.
