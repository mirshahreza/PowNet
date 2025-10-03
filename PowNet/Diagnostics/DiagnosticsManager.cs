using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PowNet.Configuration;
using PowNet.Logging;

namespace PowNet.Diagnostics
{
    /// <summary>
    /// Comprehensive diagnostics and debugging tools for PowNet framework
    /// </summary>
    public static class DiagnosticsManager
    {
        #region Performance Profiling

        private static readonly ConcurrentDictionary<string, PerformanceCounter> _counters = new();
        private static readonly ConcurrentDictionary<string, List<TimeSpan>> _measurements = new();
        private static readonly Logger _logger = PowNetLogger.GetLogger("Diagnostics");

        /// <summary>
        /// Start performance measurement
        /// </summary>
        public static IDisposable MeasurePerformance(string operationName, object? context = null)
        {
            return new PerformanceMeasurement(operationName, context);
        }

        /// <summary>
        /// Record a performance measurement
        /// </summary>
        public static void RecordMeasurement(string operationName, TimeSpan duration)
        {
            var counter = _counters.GetOrAdd(operationName, _ => new PerformanceCounter());
            counter.RecordMeasurement(duration);

            var measurements = _measurements.GetOrAdd(operationName, _ => new List<TimeSpan>());
            lock (measurements)
            {
                measurements.Add(duration);
                // Keep only last 1000 measurements
                if (measurements.Count > 1000)
                {
                    measurements.RemoveRange(0, measurements.Count - 1000);
                }
            }
        }

        /// <summary>
        /// Get performance statistics for an operation
        /// </summary>
        public static PerformanceStatistics? GetPerformanceStatistics(string operationName)
        {
            if (!_counters.TryGetValue(operationName, out var counter) || 
                !_measurements.TryGetValue(operationName, out var measurements))
            {
                return null;
            }

            lock (measurements)
            {
                if (measurements.Count == 0) return null;

                var sorted = measurements.OrderBy(m => m.Ticks).ToArray();
                
                return new PerformanceStatistics
                {
                    OperationName = operationName,
                    SampleCount = measurements.Count,
                    TotalDuration = TimeSpan.FromTicks(measurements.Sum(m => m.Ticks)),
                    AverageDuration = TimeSpan.FromTicks((long)measurements.Average(m => m.Ticks)),
                    MinDuration = sorted.First(),
                    MaxDuration = sorted.Last(),
                    MedianDuration = sorted[sorted.Length / 2],
                    P95Duration = sorted[(int)(sorted.Length * 0.95)],
                    P99Duration = sorted[(int)(sorted.Length * 0.99)]
                };
            }
        }

        /// <summary>
        /// Get all performance statistics
        /// </summary>
        public static Dictionary<string, PerformanceStatistics> GetAllPerformanceStatistics()
        {
            var results = new Dictionary<string, PerformanceStatistics>();
            
            foreach (var operationName in _counters.Keys)
            {
                var stats = GetPerformanceStatistics(operationName);
                if (stats != null)
                {
                    results[operationName] = stats;
                }
            }

            return results;
        }

        #endregion

        #region Memory Diagnostics

        /// <summary>
        /// Get current memory usage information
        /// </summary>
        public static MemoryDiagnostics GetMemoryDiagnostics()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return new MemoryDiagnostics
            {
                TotalMemory = GC.GetTotalMemory(false),
                WorkingSet = Environment.WorkingSet,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                AllocatedBytesForCurrentThread = GC.GetAllocatedBytesForCurrentThread(),
                // TotalAvailableMemoryBytes = GC.GetTotalAvailableMemoryBytes(), // Not available in all .NET versions
                HeapSizes = GetHeapSizes()
            };
        }

        /// <summary>
        /// Force garbage collection and report memory before/after
        /// </summary>
        public static MemoryCleanupReport ForceGarbageCollection()
        {
            var beforeMemory = GC.GetTotalMemory(false);
            var beforeGen0 = GC.CollectionCount(0);
            var beforeGen1 = GC.CollectionCount(1);
            var beforeGen2 = GC.CollectionCount(2);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var afterMemory = GC.GetTotalMemory(false);
            var afterGen0 = GC.CollectionCount(0);
            var afterGen1 = GC.CollectionCount(1);
            var afterGen2 = GC.CollectionCount(2);

            return new MemoryCleanupReport
            {
                MemoryBefore = beforeMemory,
                MemoryAfter = afterMemory,
                MemoryFreed = beforeMemory - afterMemory,
                Gen0Collections = afterGen0 - beforeGen0,
                Gen1Collections = afterGen1 - beforeGen1,
                Gen2Collections = afterGen2 - beforeGen2
            };
        }

        private static Dictionary<string, long> GetHeapSizes()
        {
            var info = GC.GetGCMemoryInfo();
            return new Dictionary<string, long>
            {
                ["HeapSizeBytes"] = info.HeapSizeBytes,
                ["HighMemoryLoadThresholdBytes"] = info.HighMemoryLoadThresholdBytes,
                ["MemoryLoadBytes"] = info.MemoryLoadBytes,
                // ["TotalAvailableMemoryBytes"] = info.TotalAvailableMemoryBytes, // Not available in all versions
                ["FragmentedBytes"] = info.FragmentedBytes,
                ["Index"] = info.Index,
                ["Generation"] = info.Generation
            };
        }

        #endregion

        #region Application Health

        /// <summary>
        /// Get comprehensive application health report
        /// </summary>
        public static ApplicationHealthReport GetHealthReport()
        {
            var process = Process.GetCurrentProcess();
            
            return new ApplicationHealthReport
            {
                Timestamp = DateTime.UtcNow,
                ProcessInfo = GetProcessInfo(process),
                MemoryInfo = GetMemoryDiagnostics(),
                PerformanceInfo = GetPerformanceInfo(),
                EnvironmentInfo = GetEnvironmentInfo(),
                ConfigurationInfo = GetConfigurationInfo(),
                ThreadingInfo = GetThreadingInfo()
            };
        }

        private static ProcessInfo GetProcessInfo(Process process)
        {
            return new ProcessInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                StartTime = process.StartTime,
                TotalProcessorTime = process.TotalProcessorTime,
                UserProcessorTime = process.UserProcessorTime,
                PrivilegedProcessorTime = process.PrivilegedProcessorTime,
                HandleCount = process.HandleCount,
                ThreadCount = process.Threads.Count,
                VirtualMemorySize64 = process.VirtualMemorySize64,
                WorkingSet64 = process.WorkingSet64,
                PrivateMemorySize64 = process.PrivateMemorySize64
            };
        }

        private static PerformanceInfo GetPerformanceInfo()
        {
            var allStats = GetAllPerformanceStatistics();
            
            return new PerformanceInfo
            {
                TrackedOperations = allStats.Count,
                TotalMeasurements = allStats.Values.Sum(s => s.SampleCount),
                SlowestOperation = allStats.Values.OrderByDescending(s => s.AverageDuration).FirstOrDefault()?.OperationName ?? "None",
                FastestOperation = allStats.Values.OrderBy(s => s.AverageDuration).FirstOrDefault()?.OperationName ?? "None",
                AverageOperationTime = allStats.Values.Count > 0 
                    ? TimeSpan.FromTicks((long)allStats.Values.Average(s => s.AverageDuration.Ticks))
                    : TimeSpan.Zero
            };
        }

        private static EnvironmentInfo GetEnvironmentInfo()
        {
            return new EnvironmentInfo
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                CLRVersion = Environment.Version.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                CurrentDirectory = Environment.CurrentDirectory,
                SystemDirectory = Environment.SystemDirectory,
                UserName = Environment.UserName,
                UserDomainName = Environment.UserDomainName
            };
        }

        private static ConfigurationInfo GetConfigurationInfo()
        {
            return new ConfigurationInfo
            {
                Environment = PowNetConfiguration.Environment,
                IsDevelopment = PowNetConfiguration.IsDevelopment,
                IsProduction = PowNetConfiguration.IsProduction,
                LogLevel = PowNetConfiguration.LogLevel,
                CachingEnabled = PowNetConfiguration.DefaultCacheExpirationMinutes > 0,
                FileLoggingEnabled = PowNetConfiguration.EnableFileLogging
            };
        }

        private static ThreadingInfo GetThreadingInfo()
        {
            return new ThreadingInfo
            {
                CurrentThreadId = Thread.CurrentThread.ManagedThreadId,
                ThreadPoolThreads = ThreadPool.ThreadCount,
                CompletedWorkItems = ThreadPool.CompletedWorkItemCount,
                PendingWorkItems = ThreadPool.PendingWorkItemCount
            };
        }

        #endregion

        #region Debug Utilities

        /// <summary>
        /// Conditionally execute debug code only in development
        /// </summary>
        public static void ExecuteInDevelopment(Action action)
        {
            if (PowNetConfiguration.IsDevelopment)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Development-only code execution failed");
                }
            }
        }

        /// <summary>
        /// Conditionally execute debug code with result only in development
        /// </summary>
        public static T? ExecuteInDevelopment<T>(Func<T> func, T? fallbackValue = default)
        {
            if (PowNetConfiguration.IsDevelopment)
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Development-only code execution failed");
                    return fallbackValue;
                }
            }
            return fallbackValue;
        }

        /// <summary>
        /// Assert condition in development mode
        /// </summary>
        public static void DebugAssert(bool condition, string message, 
            [CallerMemberName] string memberName = "", 
            [CallerFilePath] string fileName = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            if (PowNetConfiguration.IsDevelopment && !condition)
            {
                var assertMessage = $"Debug Assert Failed: {message} at {memberName} in {Path.GetFileName(fileName)}:{lineNumber}";
                _logger.LogError(assertMessage);
                
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        /// <summary>
        /// Dump object properties for debugging
        /// </summary>
        public static void DumpObject(object obj, string? name = null, int maxDepth = 3)
        {
            if (!PowNetConfiguration.IsDevelopment) return;

            try
            {
                var dump = ObjectDumper.Dump(obj, name ?? "Object", maxDepth);
                _logger.LogDebug("Object Dump:\n{ObjectDump}", dump);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to dump object");
            }
        }

        #endregion

        #region Stack Trace Analysis

        /// <summary>
        /// Get current call stack information
        /// </summary>
        public static CallStackInfo GetCallStack(bool skipSystemFrames = true)
        {
            var stackTrace = new StackTrace(true);
            var frames = new List<CallStackFrame>();

            for (int i = 1; i < stackTrace.FrameCount; i++) // Skip current method
            {
                var frame = stackTrace.GetFrame(i);
                if (frame == null) continue;

                var method = frame.GetMethod();
                if (method == null) continue;

                // Skip system frames if requested
                if (skipSystemFrames && IsSystemFrame(method))
                    continue;

                frames.Add(new CallStackFrame
                {
                    MethodName = method.Name,
                    ClassName = method.DeclaringType?.Name ?? "Unknown",
                    NamespaceName = method.DeclaringType?.Namespace ?? "Unknown",
                    FileName = frame.GetFileName() ?? "Unknown",
                    LineNumber = frame.GetFileLineNumber(),
                    ColumnNumber = frame.GetFileColumnNumber()
                });
            }

            return new CallStackInfo
            {
                Frames = frames,
                TotalFrames = frames.Count,
                Timestamp = DateTime.UtcNow
            };
        }

        private static bool IsSystemFrame(MethodBase method)
        {
            var typeName = method.DeclaringType?.FullName ?? "";
            return typeName.StartsWith("System.") || 
                   typeName.StartsWith("Microsoft.") ||
                   typeName.StartsWith("Newtonsoft.");
        }

        #endregion
    }

    #region Performance Measurement

    internal class PerformanceMeasurement : IDisposable
    {
        private readonly string _operationName;
        private readonly object? _context;
        private readonly Stopwatch _stopwatch;
        private readonly Logger _logger;

        public PerformanceMeasurement(string operationName, object? context)
        {
            _operationName = operationName;
            _context = context;
            _stopwatch = Stopwatch.StartNew();
            _logger = PowNetLogger.GetLogger("Performance");
            
            _logger.LogTrace("Starting measurement for operation: {Operation}", _operationName);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            DiagnosticsManager.RecordMeasurement(_operationName, _stopwatch.Elapsed);
            
            _logger.LogPerformance(_operationName, _stopwatch.Elapsed, _context);
        }
    }

    internal class PerformanceCounter
    {
        private long _count = 0;
        private long _totalTicks = 0;
        private long _minTicks = long.MaxValue;
        private long _maxTicks = long.MinValue;

        public void RecordMeasurement(TimeSpan duration)
        {
            var ticks = duration.Ticks;
            
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalTicks, ticks);
            
            // Update min/max atomically
            long currentMin, currentMax;
            do
            {
                currentMin = _minTicks;
                if (ticks >= currentMin) break;
            } while (Interlocked.CompareExchange(ref _minTicks, ticks, currentMin) != currentMin);

            do
            {
                currentMax = _maxTicks;
                if (ticks <= currentMax) break;
            } while (Interlocked.CompareExchange(ref _maxTicks, ticks, currentMax) != currentMax);
        }
    }

    #endregion

    #region Object Dumper

    internal static class ObjectDumper
    {
        public static string Dump(object obj, string name, int maxDepth)
        {
            var sb = new StringBuilder();
            DumpObject(obj, name, sb, 0, maxDepth, new HashSet<object>());
            return sb.ToString();
        }

        private static void DumpObject(object? obj, string name, StringBuilder sb, int depth, int maxDepth, HashSet<object> visited)
        {
            var indent = new string(' ', depth * 2);
            
            if (obj == null)
            {
                sb.AppendLine($"{indent}{name}: null");
                return;
            }

            if (depth >= maxDepth)
            {
                sb.AppendLine($"{indent}{name}: <max depth reached>");
                return;
            }

            var type = obj.GetType();
            
            // Handle value types and strings
            if (type.IsValueType || type == typeof(string))
            {
                sb.AppendLine($"{indent}{name}: {obj}");
                return;
            }

            // Check for circular references
            if (visited.Contains(obj))
            {
                sb.AppendLine($"{indent}{name}: <circular reference>");
                return;
            }

            visited.Add(obj);
            sb.AppendLine($"{indent}{name}: {type.Name}");

            // Handle collections
            if (obj is System.Collections.IEnumerable enumerable and not string)
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    if (index >= 10) // Limit collection items
                    {
                        sb.AppendLine($"{indent}  <{index}+ more items>");
                        break;
                    }
                    DumpObject(item, $"[{index}]", sb, depth + 1, maxDepth, visited);
                    index++;
                }
            }
            else
            {
                // Handle object properties
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties.Take(20)) // Limit properties
                {
                    try
                    {
                        if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                        {
                            var value = prop.GetValue(obj);
                            DumpObject(value, prop.Name, sb, depth + 1, maxDepth, visited);
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"{indent}  {prop.Name}: <error: {ex.Message}>");
                    }
                }
            }

            visited.Remove(obj);
        }
    }

    #endregion

    #region Data Structures

    public class PerformanceStatistics
    {
        public string OperationName { get; set; } = string.Empty;
        public int SampleCount { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public TimeSpan MedianDuration { get; set; }
        public TimeSpan P95Duration { get; set; }
        public TimeSpan P99Duration { get; set; }
    }

    public class MemoryDiagnostics
    {
        public long TotalMemory { get; set; }
        public long WorkingSet { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public long AllocatedBytesForCurrentThread { get; set; }
        // public long TotalAvailableMemoryBytes { get; set; } // Not available in all .NET versions
        public Dictionary<string, long> HeapSizes { get; set; } = new();
    }

    public class MemoryCleanupReport
    {
        public long MemoryBefore { get; set; }
        public long MemoryAfter { get; set; }
        public long MemoryFreed { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        
        public double MemoryFreedPercentage => MemoryBefore == 0 ? 0 : (double)MemoryFreed / MemoryBefore * 100;
    }

    public class ApplicationHealthReport
    {
        public DateTime Timestamp { get; set; }
        public ProcessInfo ProcessInfo { get; set; } = new();
        public MemoryDiagnostics MemoryInfo { get; set; } = new();
        public PerformanceInfo PerformanceInfo { get; set; } = new();
        public EnvironmentInfo EnvironmentInfo { get; set; } = new();
        public ConfigurationInfo ConfigurationInfo { get; set; } = new();
        public ThreadingInfo ThreadingInfo { get; set; } = new();
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public TimeSpan TotalProcessorTime { get; set; }
        public TimeSpan UserProcessorTime { get; set; }
        public TimeSpan PrivilegedProcessorTime { get; set; }
        public int HandleCount { get; set; }
        public int ThreadCount { get; set; }
        public long VirtualMemorySize64 { get; set; }
        public long WorkingSet64 { get; set; }
        public long PrivateMemorySize64 { get; set; }
    }

    public class PerformanceInfo
    {
        public int TrackedOperations { get; set; }
        public int TotalMeasurements { get; set; }
        public string SlowestOperation { get; set; } = string.Empty;
        public string FastestOperation { get; set; } = string.Empty;
        public TimeSpan AverageOperationTime { get; set; }
    }

    public class EnvironmentInfo
    {
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public string CLRVersion { get; set; } = string.Empty;
        public bool Is64BitProcess { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public string CurrentDirectory { get; set; } = string.Empty;
        public string SystemDirectory { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserDomainName { get; set; } = string.Empty;
    }

    public class ConfigurationInfo
    {
        public string Environment { get; set; } = string.Empty;
        public bool IsDevelopment { get; set; }
        public bool IsProduction { get; set; }
        public string LogLevel { get; set; } = string.Empty;
        public bool CachingEnabled { get; set; }
        public bool FileLoggingEnabled { get; set; }
    }

    public class ThreadingInfo
    {
        public int CurrentThreadId { get; set; }
        public int ThreadPoolThreads { get; set; }
        public long CompletedWorkItems { get; set; }
        public long PendingWorkItems { get; set; }
    }

    public class CallStackInfo
    {
        public List<CallStackFrame> Frames { get; set; } = new();
        public int TotalFrames { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CallStackFrame
    {
        public string MethodName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string NamespaceName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
    }

    #endregion
}