using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PowNet.Configuration;
using PowNet.Logging;
using PowNet.Diagnostics;

namespace PowNet.Development
{
    /// <summary>
    /// Development tools and utilities for PowNet framework
    /// </summary>
    public static class DevelopmentTools
    {
        #region Code Generation Helpers

        private static readonly Logger _logger = PowNetLogger.GetLogger("Development");

        /// <summary>
        /// Generate extension method stub for a type
        /// </summary>
        public static string GenerateExtensionMethod(Type targetType, string methodName, 
            string returnType = "void", string[] parameters = null!)
        {
            parameters ??= Array.Empty<string>();
            
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace PowNet.Extensions");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {targetType.Name}Extensions");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static {returnType} {methodName}(this {targetType.Name} source{(parameters.Length > 0 ? ", " + string.Join(", ", parameters) : "")})");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: Implement method logic");
            if (returnType != "void" && returnType != "Task")
            {
                sb.AppendLine($"            return default({returnType});");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Generate model class from dictionary
        /// </summary>
        public static string GenerateModelClass(string className, Dictionary<string, Type> properties, 
            string namespaceName = "PowNet.Models")
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            foreach (var prop in properties)
            {
                var typeName = GetFriendlyTypeName(prop.Value);
                sb.AppendLine($"        public {typeName} {prop.Key} {{ get; set; }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Generate configuration class stub
        /// </summary>
        public static string GenerateConfigurationClass(string className, Dictionary<string, object> defaultValues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using PowNet.Configuration;");
            sb.AppendLine();
            sb.AppendLine("namespace PowNet.Configuration");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");

            foreach (var kvp in defaultValues)
            {
                var typeName = GetFriendlyTypeName(kvp.Value.GetType());
                var defaultValueStr = FormatDefaultValue(kvp.Value);
                
                sb.AppendLine($"        public static {typeName} {kvp.Key} => PowNetConfiguration.GetConfigValue(\"{className}:{kvp.Key}\", {defaultValueStr});");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(DateTime)) return "DateTime";
            if (type == typeof(TimeSpan)) return "TimeSpan";
            
            return type.Name;
        }

        private static string FormatDefaultValue(object value)
        {
            return value switch
            {
                string s => $"\"{s}\"",
                bool b => b.ToString().ToLower(),
                null => "null",
                _ => value.ToString() ?? "null"
            };
        }

        #endregion

        #region API Documentation Generator

        /// <summary>
        /// Generate API documentation for a controller
        /// </summary>
        public static string GenerateApiDocumentation(Type controllerType)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# API Documentation for {controllerType.Name}");
            sb.AppendLine();
            
            var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.DeclaringType!.Equals(typeof(object)))
                .ToArray();

            foreach (var method in methods)
            {
                sb.AppendLine($"## {method.Name}");
                sb.AppendLine();
                
                // Method signature
                sb.AppendLine("**Signature:**");
                sb.AppendLine($"```csharp");
                sb.AppendLine($"{method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                sb.AppendLine("```");
                sb.AppendLine();

                // Parameters
                var parameters = method.GetParameters();
                if (parameters.Length > 0)
                {
                    sb.AppendLine("**Parameters:**");
                    foreach (var param in parameters)
                    {
                        sb.AppendLine($"- `{param.Name}` ({param.ParameterType.Name}): Description needed");
                    }
                    sb.AppendLine();
                }

                // Return type
                if (method.ReturnType != typeof(void))
                {
                    sb.AppendLine($"**Returns:** {method.ReturnType.Name}");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        #region Performance Benchmarking

        /// <summary>
        /// Run simple benchmark on a method
        /// </summary>
        public static BenchmarkResult RunBenchmark(string name, Action action, int iterations = 1000)
        {
            // Warm up
            for (int i = 0; i < 10; i++)
            {
                action();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                action();
            }

            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            return new BenchmarkResult
            {
                Name = name,
                Iterations = iterations,
                TotalTime = stopwatch.Elapsed,
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / iterations),
                MemoryAllocated = Math.Max(0, memoryAfter - memoryBefore),
                OperationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds
            };
        }

        /// <summary>
        /// Run benchmark on async method
        /// </summary>
        public static async Task<BenchmarkResult> RunBenchmarkAsync(string name, Func<Task> asyncAction, int iterations = 100)
        {
            // Warm up
            for (int i = 0; i < 3; i++)
            {
                await asyncAction();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                await asyncAction();
            }

            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);

            return new BenchmarkResult
            {
                Name = name,
                Iterations = iterations,
                TotalTime = stopwatch.Elapsed,
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / iterations),
                MemoryAllocated = Math.Max(0, memoryAfter - memoryBefore),
                OperationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds
            };
        }

        /// <summary>
        /// Compare performance of multiple implementations
        /// </summary>
        public static BenchmarkComparison CompareBenchmarks(Dictionary<string, Action> implementations, int iterations = 1000)
        {
            var results = new Dictionary<string, BenchmarkResult>();

            foreach (var impl in implementations)
            {
                var result = RunBenchmark(impl.Key, impl.Value, iterations);
                results[impl.Key] = result;
            }

            var fastest = results.Values.OrderBy(r => r.AverageTime).First();
            var slowest = results.Values.OrderByDescending(r => r.AverageTime).First();
            var leastMemory = results.Values.OrderBy(r => r.MemoryAllocated).First();
            var mostMemory = results.Values.OrderByDescending(r => r.MemoryAllocated).First();

            return new BenchmarkComparison
            {
                Results = results,
                Fastest = fastest,
                Slowest = slowest,
                LeastMemoryUsage = leastMemory,
                MostMemoryUsage = mostMemory,
                SpeedImprovement = slowest.AverageTime.TotalMilliseconds / fastest.AverageTime.TotalMilliseconds
            };
        }

        #endregion

        #region Development Server

        /// <summary>
        /// Development information endpoint data
        /// </summary>
        public static object GetDevelopmentInfo()
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                return new { error = "Development info only available in development environment" };
            }

            var healthReport = DiagnosticsManager.GetHealthReport();
            var performanceStats = DiagnosticsManager.GetAllPerformanceStatistics();

            return new
            {
                timestamp = DateTime.UtcNow,
                environment = new
                {
                    name = PowNetConfiguration.Environment,
                    isDevelopment = PowNetConfiguration.IsDevelopment,
                    machineName = Environment.MachineName,
                    processId = Environment.ProcessId,
                    processName = Process.GetCurrentProcess().ProcessName
                },
                performance = new
                {
                    trackedOperations = performanceStats.Count,
                    totalMeasurements = performanceStats.Values.Sum(s => s.SampleCount),
                    slowestOperations = performanceStats.Values
                        .OrderByDescending(s => s.AverageDuration)
                        .Take(5)
                        .Select(s => new { s.OperationName, AverageDurationMs = s.AverageDuration.TotalMilliseconds })
                },
                memory = new
                {
                    totalMemoryMB = healthReport.MemoryInfo.TotalMemory / 1024 / 1024,
                    workingSetMB = healthReport.MemoryInfo.WorkingSet / 1024 / 1024,
                    gen0Collections = healthReport.MemoryInfo.Gen0Collections,
                    gen1Collections = healthReport.MemoryInfo.Gen1Collections,
                    gen2Collections = healthReport.MemoryInfo.Gen2Collections
                },
                configuration = new
                {
                    logLevel = PowNetConfiguration.LogLevel,
                    enableFileLogging = PowNetConfiguration.EnableFileLogging,
                    defaultCacheExpiration = PowNetConfiguration.DefaultCacheExpirationMinutes,
                    maxConcurrentRequests = PowNetConfiguration.MaxConcurrentRequests
                }
            };
        }

        #endregion

        #region Test Data Generation

        /// <summary>
        /// Generate random test data for a type
        /// </summary>
        public static T GenerateTestData<T>() where T : new()
        {
            var instance = new T();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite);

            var random = new Random();

            foreach (var prop in properties)
            {
                try
                {
                    var value = GenerateRandomValue(prop.PropertyType, random);
                    prop.SetValue(instance, value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to generate test data for property {PropertyName}: {Error}", 
                        prop.Name, ex.Message);
                }
            }

            return instance;
        }

        /// <summary>
        /// Generate collection of test data
        /// </summary>
        public static List<T> GenerateTestDataCollection<T>(int count = 10) where T : new()
        {
            var result = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(GenerateTestData<T>());
            }
            return result;
        }

        private static object? GenerateRandomValue(Type type, Random random)
        {
            if (type == typeof(string))
                return $"TestString_{random.Next(1000)}";
            if (type == typeof(int))
                return random.Next(1, 1000);
            if (type == typeof(long))
                return (long)random.Next(1, 1000);
            if (type == typeof(bool))
                return random.Next(2) == 1;
            if (type == typeof(double))
                return random.NextDouble() * 1000;
            if (type == typeof(decimal))
                return (decimal)(random.NextDouble() * 1000);
            if (type == typeof(DateTime))
                return DateTime.Now.AddDays(random.Next(-365, 365));
            if (type == typeof(TimeSpan))
                return TimeSpan.FromMinutes(random.Next(1, 1440));
            if (type == typeof(Guid))
                return Guid.NewGuid();
            
            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                return random.Next(10) == 0 ? null : GenerateRandomValue(underlyingType!, random);
            }

            return null;
        }

        #endregion

        #region Code Analysis

        /// <summary>
        /// Analyze assembly for potential issues
        /// </summary>
        public static CodeAnalysisReport AnalyzeAssembly(Assembly assembly)
        {
            var report = new CodeAnalysisReport
            {
                AssemblyName = assembly.GetName().Name ?? "Unknown",
                AnalyzedAt = DateTime.UtcNow
            };

            var types = assembly.GetTypes();
            
            foreach (var type in types)
            {
                AnalyzeType(type, report);
            }

            return report;
        }

        private static void AnalyzeType(Type type, CodeAnalysisReport report)
        {
            // Check for large classes
            var methodCount = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length;
            var propertyCount = type.GetProperties().Length;
            
            if (methodCount > 50)
            {
                report.Issues.Add(new CodeIssue
                {
                    Severity = IssueSeverity.Warning,
                    Type = "LargeClass",
                    Message = $"Class {type.Name} has {methodCount} methods. Consider refactoring.",
                    Location = type.FullName ?? type.Name
                });
            }

            // Check for methods with many parameters
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var paramCount = method.GetParameters().Length;
                if (paramCount > 10)
                {
                    report.Issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Type = "TooManyParameters",
                        Message = $"Method {method.Name} has {paramCount} parameters. Consider refactoring.",
                        Location = $"{type.FullName}.{method.Name}"
                    });
                }
            }

            // Check for missing XML documentation (in debug builds)
            if (PowNetConfiguration.IsDevelopment && type.IsPublic)
            {
                // This is a simplified check - in reality you'd check for XML doc comments
                report.Statistics.PublicTypesWithoutDocumentation++;
            }

            report.Statistics.TotalTypes++;
            report.Statistics.TotalMethods += methodCount;
            report.Statistics.TotalProperties += propertyCount;
        }

        #endregion
    }

    #region Supporting Classes

    public class BenchmarkResult
    {
        public string Name { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public TimeSpan TotalTime { get; set; }
        public TimeSpan AverageTime { get; set; }
        public long MemoryAllocated { get; set; }
        public double OperationsPerSecond { get; set; }

        public override string ToString()
        {
            return $"{Name}: {AverageTime.TotalMilliseconds:F2}ms avg, {OperationsPerSecond:F0} ops/sec, {MemoryAllocated:N0} bytes allocated";
        }
    }

    public class BenchmarkComparison
    {
        public Dictionary<string, BenchmarkResult> Results { get; set; } = new();
        public BenchmarkResult Fastest { get; set; } = new();
        public BenchmarkResult Slowest { get; set; } = new();
        public BenchmarkResult LeastMemoryUsage { get; set; } = new();
        public BenchmarkResult MostMemoryUsage { get; set; } = new();
        public double SpeedImprovement { get; set; }

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Benchmark Comparison Summary:");
            sb.AppendLine($"Fastest: {Fastest.Name} ({Fastest.AverageTime.TotalMilliseconds:F2}ms)");
            sb.AppendLine($"Slowest: {Slowest.Name} ({Slowest.AverageTime.TotalMilliseconds:F2}ms)");
            sb.AppendLine($"Speed improvement: {SpeedImprovement:F1}x");
            sb.AppendLine($"Least memory: {LeastMemoryUsage.Name} ({LeastMemoryUsage.MemoryAllocated:N0} bytes)");
            sb.AppendLine($"Most memory: {MostMemoryUsage.Name} ({MostMemoryUsage.MemoryAllocated:N0} bytes)");
            return sb.ToString();
        }
    }

    public class CodeAnalysisReport
    {
        public string AssemblyName { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
        public List<CodeIssue> Issues { get; set; } = new();
        public CodeStatistics Statistics { get; set; } = new();

        public IEnumerable<CodeIssue> Errors => Issues.Where(i => i.Severity == IssueSeverity.Error);
        public IEnumerable<CodeIssue> Warnings => Issues.Where(i => i.Severity == IssueSeverity.Warning);
        public IEnumerable<CodeIssue> Suggestions => Issues.Where(i => i.Severity == IssueSeverity.Info);
    }

    public class CodeIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class CodeStatistics
    {
        public int TotalTypes { get; set; }
        public int TotalMethods { get; set; }
        public int TotalProperties { get; set; }
        public int PublicTypesWithoutDocumentation { get; set; }
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    #endregion
}