# DevelopmentExtensions

Advanced development / diagnostic helpers gated to Development environment or detected test context. Provides performance benchmarking, memory profiling, code quality inspection, lightweight runtime profiling, synthetic data generation, and continuous performance monitoring.

Environment Gate:
- All analysis methods skip (returning *IsAnalysisSkipped = true*) when `!PowNetConfiguration.IsDevelopment` and no test framework assemblies are detected (xUnit / *Test* pattern).

---
## Method Reference
Each method includes purpose, parameters, return shape, and a usage example.

### AnalyzeMethodPerformance<T>(this Func<T> method, int iterations = 1000, string? methodName = null, string callerName = "", string filePath = "")
Benchmarks a synchronous delegate for a fixed number of iterations capturing per?iteration execution time & allocated bytes.
Returns: `PerformanceAnalysisResult` (includes average, min, max, median, P95, memory stats, recommendations, IsAnalysisSkipped).
```csharp
var result = (() => Compute()).AnalyzeMethodPerformance(iterations:500);
if(!result.IsAnalysisSkipped)
{
    Console.WriteLine($"Avg={result.AverageExecutionTime.TotalMilliseconds:F2} ms, P95={result.P95ExecutionTime.TotalMilliseconds:F2} ms");
}
```

Parameters:
- iterations: Number of measured iterations (after a 10?iteration warmup)
- methodName: Optional override label (defaults to fileName.callerMember)

### CompareMethodImplementations<T>(Dictionary<string,Func<T>> implementations, int iterations = 1000)
Runs `AnalyzeMethodPerformance` for each named implementation and aggregates results + cross?implementation recommendations.
Returns: `MethodComparisonResult`.
```csharp
var compare = DevelopmentExtensions.CompareMethodImplementations(new(){
    ["Baseline"] = () => AlgoV1(),
    ["Optimized"] = () => AlgoV2()
}, iterations:300);
if(!compare.IsAnalysisSkipped)
{
    foreach(var kv in compare.Results) Console.WriteLine($"{kv.Key}: {kv.Value.AverageExecutionTime}");
    Console.WriteLine(string.Join("\n", compare.Recommendations));
}
```

### AnalyzeMemoryAllocations<T>(this Func<T> method, int iterations = 100)
Tracks allocation deltas + GC collection counts per generation for repeated invocations.
Returns: `MemoryAllocationAnalysis`.
```csharp
var mem = (() => BuildLargeObject()).AnalyzeMemoryAllocations(50);
if(!mem.IsAnalysisSkipped)
    Console.WriteLine($"Avg alloc = {mem.AverageAllocation/1024:F1} KB, Gen0={mem.Gen0Collections}");
```

### CreateTestFixture<T>(Func<T> factory, Action<T>? cleanup = null) where T: class
Creates a disposable wrapper running cleanup on `Dispose()`. Throws if not Development.
Returns: `TestFixture<T>` with `Instance`.
```csharp
using var fx = DevelopmentExtensions.CreateTestFixture(() => new DbContext(), c => c.Dispose());
var ctx = fx.Instance;
```

### GenerateTestData<T>(int count, Func<int,T>? customGenerator = null, Func<T,bool>? validator = null) where T: new()
Produces a list of synthetic items (either via custom generator or internal default). Stops after `count` or maxAttempts (`count * 10`). Logs warning if insufficient items could be generated.
Returns: `List<T>`.
```csharp
var sample = DevelopmentExtensions.GenerateTestData<UserDto>(100, i => new UserDto{ Id=i }, u => u.Id % 2 == 0);
```

### CreateMockDataSet<T>(int primaryCount = 100, Dictionary<string,int>? relatedCounts = null) where T: new()
Builds `MockDataSet<T>` with primary list plus related named collections (each element: anonymous `{ Id, Data }`).
Returns: `MockDataSet<T>`.
```csharp
var ds = DevelopmentExtensions.CreateMockDataSet<UserDto>(50, new(){ ["Orders"] = 10 });
```

### AnalyzeCodeQuality(Assembly assembly, string? namespaceName = null)
Scans chosen assembly (optionally scoped by namespace prefix) and aggregates statistics: total types/methods/properties, code smells, and recommendations.
Returns: `CodeQualityReport`.
```csharp
var report = DevelopmentExtensions.AnalyzeCodeQuality(typeof(SomeType).Assembly, "MyApp.Domain");
if(!report.IsAnalysisSkipped)
{
    Console.WriteLine($"Types={report.Statistics.TotalTypes}, Smells={report.CodeSmells.Count}");
}
```

### DetectCodeSmells(Type type)
Heuristic detection for LargeClass, LongParameterList, GodObject (other enum values reserved). Skips outside Development.
Returns: `List<CodeSmell>`.
```csharp
foreach(var smell in DevelopmentExtensions.DetectCodeSmells(typeof(Service)))
    Console.WriteLine(smell.Description);
```

### CreatePerformanceMonitor(string name, TimeSpan interval)
Starts a periodic timer logging GC and memory metrics at the given interval. Returns `PerformanceMonitor` (IDisposable) which should be disposed to stop collection.
```csharp
using var monitor = DevelopmentExtensions.CreatePerformanceMonitor("CoreLoop", TimeSpan.FromSeconds(5));
```

### ProfileMethod<T>(this Func<T> method, string? description = null, string methodName="", string filePath="")
Lightweight single execution profiler capturing duration, memory delta, thread id and exception (if thrown). Skipped outside Development.
Returns: `ProfiledResult<T>`.
```csharp
var prof = (() => DoWork()).ProfileMethod("Process batch");
if(!prof.IsProfilingSkipped)
    Console.WriteLine($"Took {prof.ExecutionTime.TotalMilliseconds:F2} ms (mem {prof.MemoryUsed} bytes)");
```

---
## Supporting Types
### PerformanceMeasurement
Per iteration record: `ExecutionTime`, `MemoryAllocated`, `Iteration`.

### PerformanceAnalysisResult
Key fields: `AverageExecutionTime`, `MedianExecutionTime`, `P95ExecutionTime`, `TotalMemoryAllocated`, `AverageMemoryAllocated`, `Recommendations`, `IsAnalysisSkipped`.

### MethodComparisonResult
Dictionary of named `PerformanceAnalysisResult` + aggregated `Recommendations`, `IsAnalysisSkipped`.

### MemoryAllocationAnalysis
`TotalAllocations`, `AverageAllocation`, `Gen0/Gen1/Gen2Collections`, `Recommendations`, `IsAnalysisSkipped`.

### TestFixture<T>
Disposable wrapper executing custom cleanup action.

### MockDataSet<T>
`PrimaryData` + `RelatedData` dictionary (string key ? object collection).

### CodeQualityReport / CodeStatistics / CodeSmell
Aggregated structural metrics with smell list & prioritized `Recommendations`.

### CodeSmellType
`LargeClass, LongParameterList, GodObject, DeadCode, DuplicatedCode, LongMethod` (some not yet emitted).

### Severity
`Low, Medium, High, Critical`.

### PerformanceMonitor
Timer-based GC/memory sampler (logs through `PowNetLogger`).

### ProfiledResult<T>
`Result`, `ExecutionTime`, `MemoryUsed`, `ThreadId`, `OperationName`, `Description`, `Exception`, `IsProfilingSkipped`.

---
## Example: Comparative Benchmark & Memory Check
```csharp
var cmp = DevelopmentExtensions.CompareMethodImplementations(new(){
    ["Concat"] = () => ConcatStrings(),
    ["StringBuilder"] = () => BuilderStrings()
}, iterations: 200);

if(!cmp.IsAnalysisSkipped)
{
    foreach(var r in cmp.Results) Console.WriteLine($"{r.Key}: {r.Value.AverageExecutionTime}");
}

var mem = (() => AllocateChunk()).AnalyzeMemoryAllocations(iterations: 40);
if(!mem.IsAnalysisSkipped)
    Console.WriteLine($"Avg alloc: {mem.AverageAllocation/1024:F1} KB");
```

---
## Recommendations Logic (Heuristics)
Performance suggestions trigger on:
- Avg exec time > 100 ms
- Avg allocation > 1 MB
- High variance (Max / Avg > 3)
- Total allocation > 10 MB

Memory suggestions trigger on:
- Excess Gen0 or any Gen2 collections
- High average allocation

Code quality recommendations highlight: high severity smell count, many medium smells, high average methods per class.

---
## Limitations
- Synchronous only (wrap async with `.GetAwaiter().GetResult()` or supply sync adapter)
- Allocation measurement is coarse (GC.GetTotalMemory) and affected by concurrent GC
- Code smell heuristics simplistic (does not parse syntax trees)

---
*Documentation manually crafted for clarity.*
