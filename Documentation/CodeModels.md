# CodeModels

Documentation for representative code generation / model descriptor types used to describe or emit source code constructs (classes, properties, methods, enums) for tooling and automation.

---
## Purpose
Provide lightweight in-memory representations ("models") of code elements that can be:
- Serialized (e.g., to JSON or templates)
- Transformed (add/remove members, adjust modifiers)
- Emitted as C# source strings (with a generator / formatter)

---
## Representative Types
| Type | Role |
|------|-----|
| CodeClassModel | Describes a class (Name, Namespace, Modifiers, Properties, Methods) |
| CodePropertyModel | Property metadata (Name, Type, IsNullable, DefaultValue, Attributes) |
| CodeMethodModel | Method metadata (Name, ReturnType, Parameters, Body, Access) |
| CodeEnumModel | Enum (Name, Values, UnderlyingType) |
| CodeParameterModel | Parameter (Name, Type, DefaultValue, IsOptional) |
| CodeAttributeModel | Attribute (Name, Arguments) |

(Adjust to concrete implementation names.)

---
## Example (Conceptual)
```csharp
var userModel = new CodeClassModel
{
    Namespace = "MyApp.Models",
    Name = "UserDto",
    Properties =
    {
        new CodePropertyModel { Name = "Id", Type = "int" },
        new CodePropertyModel { Name = "Name", Type = "string", IsNullable = false }
    }
};
string source = CodeEmitter.Emit(userModel); // pseudo emitter
```

---
## Typical Workflow
1. Build a tree of code models.
2. Apply transformations (naming, nullability, attribute injection).
3. Render via an emitter or templating system.
4. Optionally write to `.g.cs` for source generation.

---
## Guidance
- Keep models immutable where feasible to simplify reasoning; use builders for mutation.
- Separate concerns: modeling vs emission (no formatting logic inside model types).
- Normalize whitespace and naming before emission to avoid noisy diffs.

---
## Extension Ideas
| Area | Idea |
|------|------|
| Validation | Detect duplicate members / invalid identifiers |
| Roslyn Integration | Convert models to `SyntaxNode` trees |
| Import Management | Automatic using directive consolidation |

---
## Limitations
- Without a robust emitter, complex constructs (generics, constraints, records) may require manual handling.
