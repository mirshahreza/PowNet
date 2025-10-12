# PowNet

> Modern, modular utilities & extension framework for **.NET 10** – performance-aware, test-backed, extensible.

<div align="center">

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](#) [![Tests](https://img.shields.io/badge/tests-403-green.svg)](#) [![License](https://img.shields.io/badge/license-MIT-blue.svg)](#license) [![Target](https://img.shields.io/badge/.NET-10.0-informational.svg)](#requirements)

</div>

---
## Quick Navigation
- [Overview](#overview)
- [Key Features](#key-features)
- [Why PowNet?](#why-pownet)
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Data Layer (DbCommandExecutor)](#data-layer-dbcommandexecutor)
- [Performance & Diagnostics](#performance--diagnostics)
- [Architecture](#architecture)
- [Examples](#examples)
- [Documentation Index](#documentation)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

---
## Overview
PowNet is a lightweight yet comprehensive extension & utility toolkit for building high-quality .NET backends, tools, services and test harnesses. It focuses on:
- Developer ergonomics
- Observability (diagnostics, profiling hooks, performance measurement)
- Configuration clarity
- Safe, explicit patterns (logging, retries, structured exceptions)

All modules are covered by unit tests (currently **403 passing tests**).

---
## Key Features
| Area | Highlights | Notes |
|------|------------|-------|
| Collections | Batch ops, safe parallel processing, partitioning, transformation helpers | `CollectionExtensions`, `ListExtensions` |
| Configuration | Hierarchical resolution chain, runtime overrides, backups, environment awareness | `PowNetConfiguration` |
| Security | Hashing helpers, crypto utils, middleware-friendly patterns | `SecurityExtensions`, `AdvancedSecurityTools` |
| Diagnostics | Performance timers, profiling wrappers, code quality + allocation analysis | `DevelopmentExtensions` |
| Eventing | In-process event bus with retry, filtering, pipelines, debouncing, aggregation | `EventBusExtensions` |
| Data Access | `DbCommandExecutor` facade (formerly `DbIO`) using SqlClient by default, execution hooks, transactions | See docs below |
| Caching | Simple primitives & cache orchestration patterns | `CacheExtensions` |
| Code Generation | Extension method, model, configuration & benchmark scaffolding | `DevelopmentTools` |
| Exceptions | Rich contextual exception builder (`PowNetException`) | Structured metadata |
| JSON / Text | High-performance helpers for JSON merge, clone, diff | `JsonExtensions` |
| Date/Time | UTC safety, range, window & schedule utils | `DateTimeExtensions` |

---
## Why PowNet?
- Avoid boilerplate: Unified wrappers for data, events, diagnostics.
- Observability built-in: hooks for logging & metrics (no hard dependency on a single logging framework).
- Production-ready defaults with development affordances.
- Explicit, documented patterns (architecture + usage docs per feature).
- Framework-agnostic: use in console apps, services, tests, microservices.

---
## Installation
Currently consumed via project reference (monorepo style):
```bash
git clone https://github.com/mirshahreza/PowNet.git
cd PowNet
# build
 dotnet build
# run tests
 dotnet test
```
(Optional) Create a NuGet package:
```bash
dotnet pack PowNet/PowNet.csproj -c Release
```
Add reference in your solution:
```bash
dotnet add <YourProject>.csproj reference PowNet/PowNet.csproj
```

---
## Getting Started
```csharp
using PowNet.Extensions;

// Collection helper
var items = new[] {1,2,3,4};
var batched = items.Batch(2); // [[1,2],[3,4]]

// Event bus quick usage
public record UserCreated(int Id);
EventBusExtensions.Subscribe<UserCreated>((e, ct) => { Console.WriteLine($"User {e.Id}"); return Task.CompletedTask; });
await new UserCreated(10).PublishAsync();

// Configuration access
int timeout = PowNetConfiguration.GetConfigValue("PowNet:Database:CommandTimeout", 30);
```

---
## Data Layer (DbCommandExecutor)
`DbCommandExecutor` abstracts ADO.NET interaction (formerly `DbIO`):
- Execution wrappers (sync & async) with timing + exception enrichment
- Transaction helpers (`BeginTransaction`, `CommitTransaction`, `RollbackTransaction`)
- Extensible provider model (add PostgreSQL/MySQL by subclassing `DbIO`)
- Hooks: `OnBeforeExecute`, `OnAfterExecute`

Example:
```csharp
using var db = DbIO.Instance("DefaultConnection");
var userCount = (int)(db.ExecuteScalar("SELECT COUNT(1) FROM Users") ?? 0);
```
More: [DbIO Overview](Documentation/DbIO.md) · [Design](Documentation/DbIO.Design.md) · [Usage](Documentation/DbIO.Usage.md)

---
## Performance & Diagnostics
The `DevelopmentExtensions` module provides:
- Method performance sampling (`AnalyzeMethodPerformance`)
- Allocation analysis (`AnalyzeMemoryAllocations`)
- Code quality and smell detection (`AnalyzeCodeQuality`)
- Benchmark + comparison utilities (`CompareMethodImplementations`, `CompareBenchmarks`)

Example:
```csharp
Func<int> add = () => 1+1;
var perf = add.AnalyzeMethodPerformance(iterations: 100);
if (!perf.IsAnalysisSkipped) Console.WriteLine(perf.AverageExecutionTime);
```

---
## Architecture
```
PowNet
 |- Configuration (PowNetConfiguration, EnvironmentManager)
 |- Extensions
 |  |- Collections & Concurrency
 |  |- Diagnostics & Development
 |  |- EventBus (retry, pipeline, debounce)
 |  |- Security & Encryption
 |  |- Data (DbIO + provider)
 |  |- JSON / Text / Date / Object
 |- Data (Providers, Abstract Facade)
 |- Logging (PowNetLogger integration points)
 |- Tests (395 validated scenarios)
```
Design documents: see [Documentation/INDEX](Documentation/INDEX.md).

---
## Examples
| Scenario | Snippet |
|----------|---------|
| Retry publish | `await evt.PublishWithRetryAsync(maxRetries:3)` |
| Schedule event | `evt.ScheduleEvent(DateTime.UtcNow.AddSeconds(30));` |
| Transaction | `db.BeginTransaction(); ... db.CommitTransaction();` |
| Generate test data | `DevelopmentExtensions.GenerateTestData<MyType>(100);` |
| Code smell scan | `DevelopmentExtensions.DetectCodeSmells(typeof(MyType));` |
| Profile method | `var r = (() => Calc()).ProfileMethod();` |

---
## Documentation
Central docs live in the `Documentation/` folder (each feature isolated). Start here:
- [Full Index](Documentation/INDEX.md)
- Popular entries:
  - [PowNetConfiguration](Documentation/PowNetConfiguration.md)
  - [EventBusExtensions](Documentation/EventBusExtensions.md)
  - [PerformanceExtensions](Documentation/PerformanceExtensions.md)
- [DbCommandExecutor](Documentation/DbIO.md)
  - [SecurityExtensions](Documentation/SecurityExtensions.md)

---
## Roadmap
| Status | Item |
|--------|------|
| Planned | Additional DB providers (PostgreSQL, MySQL) |
| Planned | `IDbIO` interface + DI adapters |
| Planned | Retry / circuit breaker integration (Polly) |
| In Progress | Source generator for POCO mapping from `DbDataReader` |
| Planned | Bulk operations (`SqlBulkCopy` wrapper) |
| Planned | Metrics enrichment (row counts, latency histograms) |

Feel free to open issues for proposals.

---
## Contributing
1. Fork & branch (`feat/<name>` or `fix/<issue>`).
2. Add/adjust tests (no feature merged without coverage).
3. Follow existing naming & folder patterns.
4. Run: `dotnet format` (if configured) & `dotnet test`.
5. Submit PR with concise description + motivation.

### Development Guidelines
- Keep extension methods focused & side-effect free.
- Prefer explicit configuration keys (`PowNet:Section:Key`).
- Avoid static mutable state unless guarded (see EventBus reset).
- Add design notes for complex subsystems (`Documentation/*.md`).

---
## License
MIT – see [LICENSE](LICENSE).

---
*Generated & curated; contributions to improve clarity or performance patterns are welcome.*
