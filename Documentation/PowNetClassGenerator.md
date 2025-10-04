# PowNetClassGenerator

Utility for programmatically generating simple class source code (name + properties) to accelerate repetitive DTO / model creation.

---
## Representative Method
### Generate(string className, IEnumerable<(string Name,string Type)> properties, string? @namespace = null)
Returns C# code string containing class definition with auto-properties.

```csharp
string code = PowNetClassGenerator.Generate(
    "UserDto",
    new[]{("Id","int"), ("Name","string"), ("Created","DateTime")},
    "MyApp.Models");
File.WriteAllText("UserDto.g.cs", code);
```

---
## Notes
- Minimal validation; ensure property identifiers are valid C# names.
- Extend to support attributes, inheritance, or nullable reference type annotations.

---
## Extension Ideas
| Feature | Idea |
|---------|------|
| Annotations | Accept attribute list per property |
| Constructors | Generate parameterized ctor |
| Records | Option to emit `record` instead of `class` |
