# DevelopmentTools

Generation, benchmarking, diagnostics and lightweight analysis utilities that complement `DevelopmentExtensions`. All methods are static and intended for Dev / internal tooling.

---
## 1. Code Generation Helpers
### GenerateExtensionMethod(Type targetType, string methodName, string returnType = "void", string[] parameters = null)
Creates a C# source stub for an extension method on `targetType`.
Returns: `string` (code snippet)
```csharp
string stub = DevelopmentTools.GenerateExtensionMethod(typeof(string), "ToRev", "string", new[]{"int count"});
```

### GenerateModelClass(string className, Dictionary<string,Type> properties, string namespaceName = "PowNet.Models")
Produces a POCO class with auto-properties.
```csharp
var code = DevelopmentTools.GenerateModelClass("UserDto", new(){["Id"] = typeof(int), ["Name"] = typeof(string)});
```

### GenerateConfigurationClass(string className, Dictionary<string,object> defaultValues)
Static accessor class referencing `PowNetConfiguration` for each default.
```csharp
var cfgSrc = DevelopmentTools.GenerateConfigurationClass("MyFeature", new(){["Enabled"] = true, ["Max"] = 10});
```

---
## 2. API Documentation
### GenerateApiDocumentation(Type controllerType)
Reflection-based Markdown for public instance methods.
```csharp
string md = DevelopmentTools.GenerateApiDocumentation(typeof(MyController));
```

---
## 3. Benchmarking
### RunBenchmark(string name, Action action, int iterations = 1000)
Warmup (10) then measure aggregate & average + memory.
```csharp
var bench = DevelopmentTools.RunBenchmark("Concat", () => Concat(), 400);
```

### RunBenchmarkAsync(string name, Func<Task> asyncAction, int iterations = 100)
Async variant (warmup 3).
```csharp
var asyncBench = await DevelopmentTools.RunBenchmarkAsync("Fetch", async () => await ApiCall());
```

### CompareBenchmarks(Dictionary<string,Action> implementations, int iterations = 1000)
Aggregates `BenchmarkResult` per name, plus fastest/slowest.
```csharp
var cmp = DevelopmentTools.CompareBenchmarks(new(){["A"] = () => A(), ["B"] = () => B()});
Console.WriteLine(cmp.GetSummary());
```

---
## 4. Development Info
### GetDevelopmentInfo()
Returns anonymous object aggregating environment, performance stats (`DiagnosticsManager`), memory, config. Error object when not Development.
```csharp
var info = DevelopmentTools.GetDevelopmentInfo();
```

---
## 5. Test Data Generation
### GenerateTestData<T>() where T:new()
Populates an instance with random primitive values.
```csharp
var sample = DevelopmentTools.GenerateTestData<UserDto>();
```

### GenerateTestDataCollection<T>(int count = 10) where T:new()
Batch generator.
```csharp
var list = DevelopmentTools.GenerateTestDataCollection<UserDto>(25);
```

---
## 6. Assembly Analysis
### AnalyzeAssembly(Assembly assembly)
Heuristic structural scan producing `CodeAnalysisReport` (issues & stats).
```csharp
var rep = DevelopmentTools.AnalyzeAssembly(typeof(UserDto).Assembly);
```

---
## 7. Supporting Types
### BenchmarkResult
| Field | Meaning |
| Name | identifier |
| Iterations | loop count |
| TotalTime | cumulative |
| AverageTime | per iteration |
| MemoryAllocated | net bytes |
| OperationsPerSecond | throughput |

### BenchmarkComparison
Holds result map + summary properties (Fastest/Slowest/LeastMemory/MostMemory) & `SpeedImprovement` ratio.

### CodeAnalysisReport / CodeIssue / CodeStatistics
Structural metrics & issues (LargeClass, TooManyParameters, missing docs marker).

### IssueSeverity
`Info, Warning, Error`.

---
## Example
```csharp
var modelSrc = DevelopmentTools.GenerateModelClass("Invoice", new(){["Id"] = typeof(int), ["Amount"] = typeof(decimal)});
var comparison = DevelopmentTools.CompareBenchmarks(new(){
  ["Loop1"] = () => V1(),
  ["Loop2"] = () => V2()
}, 300);
Console.WriteLine(comparison.GetSummary());
```

---
## Limitations
- No statistical warmup trimming / outlier analysis.
- Random generator does not traverse complex graphs.
- API doc generator ignores attributes (routing, verbs).

---
*Manual documentation.*
