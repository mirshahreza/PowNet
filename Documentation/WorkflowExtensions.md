# WorkflowExtensions

Helpers for building simple in-process workflows / pipelines: step chaining, conditional branching, compensation (rollback) actions, and execution diagnostics.

---
## Capabilities
- Define ordered steps each operating on shared context state
- Short-circuit on failure (Result pattern)
- Optional compensation actions for rollback when later steps fail
- Branch / conditional execution (if implemented)

---
## Representative API (Illustrative)
```csharp
var workflow = WorkflowBuilder<ImportContext>
    .Create()
    .AddStep("Validate", ctx => Validate(ctx))
    .AddStep("Transform", ctx => Transform(ctx))
    .AddStep("Persist", ctx => Save(ctx))
    .Build();

var result = await workflow.ExecuteAsync(new ImportContext());
```

---
## Guidance
- Keep steps idempotent to allow safe retries.
- Use clear step names for diagnostic logging.

---
## Extension Ideas
| Feature | Idea |
|---------|------|
| Parallel branches | Execute independent steps concurrently |
| Timeout per step | Cancel long-running operations |
| Metrics | Emit duration per step |

---
## Limitations
- Not a substitute for a full BPM/workflow engine (no persistence / state machine features).
