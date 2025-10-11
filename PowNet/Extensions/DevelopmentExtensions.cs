using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PowNet.Configuration;
using PowNet.Logging;

namespace PowNet.Extensions
{
    /// <summary>
    /// Advanced development and testing utilities for PowNet framework
    /// </summary>
    public static class DevelopmentExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("Development");

        private static bool AllowInTestContext()
        {
            // Detect xUnit or common test frameworks loaded
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            return loaded.Any(a => a.FullName != null && (a.FullName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) || a.FullName.Contains("Test", StringComparison.OrdinalIgnoreCase)));
        }

        #region Code Analysis Extensions

        /// <summary>
        /// Analyze method performance and suggest optimizations
        /// </summary>
        public static PerformanceAnalysisResult AnalyzeMethodPerformance<T>(
            this Func<T> method,
            int iterations = 1000,
            string? methodName = null,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "")
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                return new PerformanceAnalysisResult { IsAnalysisSkipped = true };
            }

            var actualMethodName = methodName ?? $"{Path.GetFileNameWithoutExtension(filePath)}.{callerName}";
            var results = new List<PerformanceMeasurement>();

            // Warm up
            for (int i = 0; i < 10; i++)
            {
                method();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure iterations
            for (int i = 0; i < iterations; i++)
            {
                var memoryBefore = GC.GetTotalMemory(false);
                var sw = Stopwatch.StartNew();

                var result = method();

                sw.Stop();
                var memoryAfter = GC.GetTotalMemory(false);

                results.Add(new PerformanceMeasurement
                {
                    ExecutionTime = sw.Elapsed,
                    MemoryAllocated = Math.Max(0, memoryAfter - memoryBefore),
                    Iteration = i + 1
                });
            }

            return AnalyzeResults(actualMethodName, results);
        }

        /// <summary>
        /// Compare multiple method implementations
        /// </summary>
        public static MethodComparisonResult CompareMethodImplementations<T>(
            Dictionary<string, Func<T>> implementations,
            int iterations = 1000)
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                return new MethodComparisonResult { IsAnalysisSkipped = true };
            }

            var results = new Dictionary<string, PerformanceAnalysisResult>();

            foreach (var implementation in implementations)
            {
                var result = AnalyzeMethodPerformance(implementation.Value, iterations, implementation.Key);
                results[implementation.Key] = result;
            }

            return new MethodComparisonResult
            {
                Results = results,
                Recommendations = GenerateOptimizationRecommendations(results)
            };
        }

        /// <summary>
        /// Analyze memory allocation patterns
        /// </summary>
        public static MemoryAllocationAnalysis AnalyzeMemoryAllocations<T>(
            this Func<T> method,
            int iterations = 100)
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                return new MemoryAllocationAnalysis { IsAnalysisSkipped = true };
            }

            var allocations = new List<long>();
            var gcCounts = new List<(int Gen0, int Gen1, int Gen2)>();

            for (int i = 0; i < iterations; i++)
            {
                var gen0Before = GC.CollectionCount(0);
                var gen1Before = GC.CollectionCount(1);
                var gen2Before = GC.CollectionCount(2);
                var memoryBefore = GC.GetTotalMemory(false);

                method();

                var memoryAfter = GC.GetTotalMemory(false);
                var gen0After = GC.CollectionCount(0);
                var gen1After = GC.CollectionCount(1);
                var gen2After = GC.CollectionCount(2);

                allocations.Add(Math.Max(0, memoryAfter - memoryBefore));
                gcCounts.Add((gen0After - gen0Before, gen1After - gen1Before, gen2After - gen2Before));
            }

            return new MemoryAllocationAnalysis
            {
                TotalAllocations = allocations.Sum(),
                AverageAllocation = allocations.Average(),
                MaxAllocation = allocations.Max(),
                MinAllocation = allocations.Min(),
                Gen0Collections = gcCounts.Sum(gc => gc.Gen0),
                Gen1Collections = gcCounts.Sum(gc => gc.Gen1),
                Gen2Collections = gcCounts.Sum(gc => gc.Gen2),
                Recommendations = GenerateMemoryRecommendations(allocations, gcCounts)
            };
        }

        #endregion

        #region Testing Utilities

        /// <summary>
        /// Create test fixture with automatic cleanup
        /// </summary>
        public static TestFixture<T> CreateTestFixture<T>(
            Func<T> factory,
            Action<T>? cleanup = null) where T : class
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                throw new InvalidOperationException("Test fixtures are only available in development environment");
            }

            return new TestFixture<T>(factory, cleanup);
        }

        /// <summary>
        /// Generate test data with constraints
        /// </summary>
        public static List<T> GenerateTestData<T>(
            int count,
            Func<int, T>? customGenerator = null,
            Func<T, bool>? validator = null) where T : new()
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                throw new InvalidOperationException("Test data generation is only available in development environment");
            }

            var result = new List<T>();
            var attempts = 0;
            var maxAttempts = count * 10;

            while (result.Count < count && attempts < maxAttempts)
            {
                T item;
                
                if (customGenerator != null)
                {
                    item = customGenerator(result.Count);
                }
                else
                {
                    item = Development.DevelopmentTools.GenerateTestData<T>();
                }

                if (validator == null || validator(item))
                {
                    result.Add(item);
                }

                attempts++;
            }

            if (result.Count < count)
            {
                _logger.LogWarning("Could only generate {ActualCount} out of {RequestedCount} test items after {Attempts} attempts",
                    result.Count, count, attempts);
            }

            return result;
        }

        /// <summary>
        /// Create mock data with realistic relationships
        /// </summary>
        public static MockDataSet<T> CreateMockDataSet<T>(
            int primaryCount = 100,
            Dictionary<string, int>? relatedCounts = null) where T : new()
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                throw new InvalidOperationException("Mock data creation is only available in development environment");
            }

            var dataSet = new MockDataSet<T>
            {
                PrimaryData = GenerateTestData<T>(primaryCount)
            };

            if (relatedCounts != null)
            {
                foreach (var related in relatedCounts)
                {
                    dataSet.RelatedData[related.Key] = Enumerable.Range(0, related.Value)
                        .Select(_ => new { Id = Guid.NewGuid(), Data = "MockData" })
                        .ToList();
                }
            }

            return dataSet;
        }

        #endregion

        #region Code Quality Analysis

        /// <summary>
        /// Analyze code complexity and suggest improvements
        /// </summary>
        public static CodeQualityReport AnalyzeCodeQuality(Assembly assembly, string? namespaceName = null)
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                return new CodeQualityReport { IsAnalysisSkipped = true };
            }

            var report = new CodeQualityReport
            {
                AssemblyName = assembly.GetName().Name ?? "Unknown",
                AnalyzedAt = DateTime.UtcNow
            };

            var types = string.IsNullOrEmpty(namespaceName)
                ? assembly.GetTypes()
                : assembly.GetTypes().Where(t => t.Namespace?.StartsWith(namespaceName) == true);

            foreach (var type in types)
            {
                AnalyzeType(type, report);
            }

            GenerateQualityRecommendations(report);
            return report;
        }

        /// <summary>
        /// Detect code smells and anti-patterns
        /// </summary>
        public static List<CodeSmell> DetectCodeSmells(Type type)
        {
            if (!PowNetConfiguration.IsDevelopment && !AllowInTestContext())
            {
                return new List<CodeSmell>();
            }

            var smells = new List<CodeSmell>();

            // Large class detection
            var methodCount = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length;
            if (methodCount > 50)
            {
                smells.Add(new CodeSmell
                {
                    Type = CodeSmellType.LargeClass,
                    Severity = Severity.Medium,
                    Description = $"Class has {methodCount} methods. Consider breaking into smaller classes.",
                    Location = type.FullName ?? type.Name
                });
            }

            // Long parameter list detection
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var paramCount = method.GetParameters().Length;
                if (paramCount > 7)
                {
                    smells.Add(new CodeSmell
                    {
                        Type = CodeSmellType.LongParameterList,
                        Severity = Severity.Low,
                        Description = $"Method '{method.Name}' has {paramCount} parameters. Consider parameter object pattern.",
                        Location = $"{type.FullName}.{method.Name}"
                    });
                }
            }

            // God object detection
            var propertyCount = type.GetProperties().Length;
            if (methodCount > 30 && propertyCount > 20)
            {
                smells.Add(new CodeSmell
                {
                    Type = CodeSmellType.GodObject,
                    Severity = Severity.High,
                    Description = "Class appears to be a 'God Object' with too many responsibilities.",
                    Location = type.FullName ?? type.Name
                });
            }

            return smells;
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Monitor application performance in real-time
        /// </summary>
        public static PerformanceMonitor CreatePerformanceMonitor(string name, TimeSpan interval)
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                throw new InvalidOperationException("Performance monitoring is only available in development environment");
            }

            return new PerformanceMonitor(name, interval);
        }

        /// <summary>
        /// Profile method calls with detailed analysis
        /// </summary>
        public static ProfiledResult<T> ProfileMethod<T>(
            this Func<T> method,
            string? description = null,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string filePath = "")
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                return new ProfiledResult<T> { Result = method(), IsProfilingSkipped = true };
            }

            var operationName = $"{Path.GetFileNameWithoutExtension(filePath)}.{methodName}";
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            var threadId = Thread.CurrentThread.ManagedThreadId;

            Exception? exception = null;
            T result = default!;

            try
            {
                result = method();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = Math.Max(0, memoryAfter - memoryBefore);

                _logger.LogStructured(LogLevel.Debug, "Method profiling completed",
                    new
                    {
                        Operation = operationName,
                        Description = description,
                        Duration = stopwatch.Elapsed,
                        MemoryUsed = memoryUsed,
                        ThreadId = threadId,
                        Success = exception == null,
                        Exception = exception?.GetType().Name
                    });
            }

            return new ProfiledResult<T>
            {
                Result = result,
                ExecutionTime = stopwatch.Elapsed,
                MemoryUsed = GC.GetTotalMemory(false) - memoryBefore,
                ThreadId = threadId,
                OperationName = operationName,
                Description = description,
                Exception = exception
            };
        }

        #endregion

        #region Private Helper Methods

        private static PerformanceAnalysisResult AnalyzeResults(string methodName, List<PerformanceMeasurement> results)
        {
            var executionTimes = results.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
            var memoryAllocations = results.Select(r => r.MemoryAllocated).ToArray();

            Array.Sort(executionTimes);
            Array.Sort(memoryAllocations);

            var analysis = new PerformanceAnalysisResult
            {
                MethodName = methodName,
                SampleCount = results.Count,
                AverageExecutionTime = TimeSpan.FromMilliseconds(executionTimes.Average()),
                MinExecutionTime = TimeSpan.FromMilliseconds(executionTimes.First()),
                MaxExecutionTime = TimeSpan.FromMilliseconds(executionTimes.Last()),
                MedianExecutionTime = TimeSpan.FromMilliseconds(executionTimes[executionTimes.Length / 2]),
                P95ExecutionTime = TimeSpan.FromMilliseconds(executionTimes[(int)(executionTimes.Length * 0.95)]),
                TotalMemoryAllocated = memoryAllocations.Sum(),
                AverageMemoryAllocated = memoryAllocations.Average(),
                MaxMemoryAllocated = memoryAllocations.Max()
            };

            analysis.Recommendations = GeneratePerformanceRecommendations(analysis);
            return analysis;
        }

        private static List<string> GeneratePerformanceRecommendations(PerformanceAnalysisResult analysis)
        {
            var recommendations = new List<string>();

            if (analysis.AverageExecutionTime.TotalMilliseconds > 100)
            {
                recommendations.Add("Consider optimizing algorithm complexity - average execution time is high");
            }

            if (analysis.AverageMemoryAllocated > 1024 * 1024) // 1MB
            {
                recommendations.Add("High memory allocation detected - consider object pooling or reusing objects");
            }

            if (analysis.MaxExecutionTime.TotalMilliseconds / analysis.AverageExecutionTime.TotalMilliseconds > 3)
            {
                recommendations.Add("High execution time variance - investigate performance bottlenecks");
            }

            if (analysis.TotalMemoryAllocated > 10 * 1024 * 1024) // 10MB total
            {
                recommendations.Add("Total memory allocation is high - review memory usage patterns");
            }

            return recommendations;
        }

        private static List<string> GenerateOptimizationRecommendations(Dictionary<string, PerformanceAnalysisResult> results)
        {
            var recommendations = new List<string>();

            var fastestMethod = results.Values.OrderBy(r => r.AverageExecutionTime).First();
            var slowestMethod = results.Values.OrderByDescending(r => r.AverageExecutionTime).First();

            var speedDifference = slowestMethod.AverageExecutionTime.TotalMilliseconds / fastestMethod.AverageExecutionTime.TotalMilliseconds;

            if (speedDifference > 2)
            {
                recommendations.Add($"'{fastestMethod.MethodName}' is {speedDifference:F1}x faster than '{slowestMethod.MethodName}'");
            }

            return recommendations;
        }

        private static List<string> GenerateMemoryRecommendations(List<long> allocations, List<(int Gen0, int Gen1, int Gen2)> gcCounts)
        {
            var recommendations = new List<string>();

            var totalGen0 = gcCounts.Sum(gc => gc.Gen0);
            var totalGen1 = gcCounts.Sum(gc => gc.Gen1);
            var totalGen2 = gcCounts.Sum(gc => gc.Gen2);

            if (totalGen0 > allocations.Count * 0.1)
            {
                recommendations.Add("High Gen0 garbage collections - consider reducing object allocations");
            }

            if (totalGen2 > 0)
            {
                recommendations.Add("Gen2 garbage collections detected - review long-lived object usage");
            }

            if (allocations.Average() > 1024 * 1024) // 1MB average
            {
                recommendations.Add("High average memory allocation - consider object pooling");
            }

            return recommendations;
        }

        private static void AnalyzeType(Type type, CodeQualityReport report)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var properties = type.GetProperties();
            
            report.Statistics.TotalTypes++;
            report.Statistics.TotalMethods += methods.Length;
            report.Statistics.TotalProperties += properties.Length;

            var smells = DetectCodeSmells(type);
            report.CodeSmells.AddRange(smells);
        }

        private static void GenerateQualityRecommendations(CodeQualityReport report)
        {
            var highSeveritySmells = report.CodeSmells.Count(cs => cs.Severity == Severity.High);
            var mediumSeveritySmells = report.CodeSmells.Count(cs => cs.Severity == Severity.Medium);

            if (highSeveritySmells > 0)
            {
                report.Recommendations.Add($"Address {highSeveritySmells} high-severity code smells immediately");
            }

            if (mediumSeveritySmells > 5)
            {
                report.Recommendations.Add($"Consider refactoring to address {mediumSeveritySmells} medium-severity issues");
            }

            var avgMethodsPerClass = (double)report.Statistics.TotalMethods / report.Statistics.TotalTypes;
            if (avgMethodsPerClass > 20)
            {
                report.Recommendations.Add("Consider breaking large classes into smaller, more focused classes");
            }
        }

        #endregion
    }

    #region Supporting Classes and Enums

    public class PerformanceMeasurement
    {
        public TimeSpan ExecutionTime { get; set; }
        public long MemoryAllocated { get; set; }
        public int Iteration { get; set; }
    }

    public class PerformanceAnalysisResult
    {
        public string MethodName { get; set; } = string.Empty;
        public int SampleCount { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan MinExecutionTime { get; set; }
        public TimeSpan MaxExecutionTime { get; set; }
        public TimeSpan MedianExecutionTime { get; set; }
        public TimeSpan P95ExecutionTime { get; set; }
        public long TotalMemoryAllocated { get; set; }
        public double AverageMemoryAllocated { get; set; }
        public long MaxMemoryAllocated { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public bool IsAnalysisSkipped { get; set; }
    }

    public class MethodComparisonResult
    {
        public Dictionary<string, PerformanceAnalysisResult> Results { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public bool IsAnalysisSkipped { get; set; }
    }

    public class MemoryAllocationAnalysis
    {
        public long TotalAllocations { get; set; }
        public double AverageAllocation { get; set; }
        public long MaxAllocation { get; set; }
        public long MinAllocation { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public bool IsAnalysisSkipped { get; set; }
    }

    public class TestFixture<T> : IDisposable where T : class
    {
        private readonly Action<T>? _cleanup;
        public T Instance { get; }

        public TestFixture(Func<T> factory, Action<T>? cleanup = null)
        {
            _cleanup = cleanup;
            Instance = factory();
        }

        public void Dispose()
        {
            _cleanup?.Invoke(Instance);
        }
    }

    public class MockDataSet<T>
    {
        public List<T> PrimaryData { get; set; } = new();
        public Dictionary<string, object> RelatedData { get; set; } = new();
    }

    public class CodeQualityReport
    {
        public string AssemblyName { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        public CodeStatistics Statistics { get; set; } = new();
        public List<CodeSmell> CodeSmells { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public bool IsAnalysisSkipped { get; set; }
    }

    public class CodeStatistics
    {
        public int TotalTypes { get; set; }
        public int TotalMethods { get; set; }
        public int TotalProperties { get; set; }
    }

    public class CodeSmell
    {
        public CodeSmellType Type { get; set; }
        public Severity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public enum CodeSmellType
    {
        LargeClass,
        LongParameterList,
        GodObject,
        DeadCode,
        DuplicatedCode,
        LongMethod
    }

    public enum Severity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class PerformanceMonitor : IDisposable
    {
        private readonly Timer _timer;
        private readonly string _name;
        private bool _disposed;

        public PerformanceMonitor(string name, TimeSpan interval)
        {
            _name = name;
            _timer = new Timer(CollectMetrics, null, TimeSpan.Zero, interval);
        }

        private void CollectMetrics(object? state)
        {
            if (_disposed) return;

            var memory = GC.GetTotalMemory(false);
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            var logger = PowNetLogger.GetLogger("PerformanceMonitor");
            logger.LogDebug("Performance metrics for {MonitorName}: Memory={Memory}, GC0={Gen0}, GC1={Gen1}, GC2={Gen2}",
                _name, memory, gen0, gen1, gen2);
        }

        public void Dispose()
        {
            _disposed = true;
            _timer?.Dispose();
        }
    }

    public class ProfiledResult<T>
    {
        public T Result { get; set; } = default!;
        public TimeSpan ExecutionTime { get; set; }
        public long MemoryUsed { get; set; }
        public int ThreadId { get; set; }
        public string OperationName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Exception? Exception { get; set; }
        public bool IsProfilingSkipped { get; set; }
    }

    #endregion
}