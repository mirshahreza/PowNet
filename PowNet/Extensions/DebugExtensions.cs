using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using PowNet.Configuration;
using PowNet.Logging;
using PowNet.Common;

namespace PowNet.Extensions
{
    /// <summary>
    /// Debug and profiling extensions for PowNet framework
    /// </summary>
    public static class DebugExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("Debug");

        #region Method Profiling Extensions

        /// <summary>
        /// Profile method execution time and log results
        /// </summary>
        public static T Profile<T>(this Func<T> func, 
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return func();

            var methodInfo = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:{lineNumber}";
            
            using var measurement = Diagnostics.DiagnosticsManager.MeasurePerformance($"Profile_{methodInfo}");
            var sw = Stopwatch.StartNew();
            
            try
            {
                var result = func();
                sw.Stop();
                
                _logger.LogDebug("Method {Method} executed in {Duration}ms", 
                    methodInfo, sw.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError("Method {Method} failed after {Duration}ms: {Error}", 
                    methodInfo, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Profile async method execution
        /// </summary>
        public static async Task<T> ProfileAsync<T>(this Func<Task<T>> func,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return await func();

            var methodInfo = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:{lineNumber}";
            
            using var measurement = Diagnostics.DiagnosticsManager.MeasurePerformance($"ProfileAsync_{methodInfo}");
            var sw = Stopwatch.StartNew();
            
            try
            {
                var result = await func();
                sw.Stop();
                
                _logger.LogDebug("Async method {Method} executed in {Duration}ms", 
                    methodInfo, sw.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError("Async method {Method} failed after {Duration}ms: {Error}", 
                    methodInfo, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        #endregion

        #region Object Debugging Extensions

        /// <summary>
        /// Debug dump object properties
        /// </summary>
        public static T DebugDump<T>(this T obj, string? name = null,
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return obj;

            try
            {
                var objectName = name ?? typeof(T).Name;
                var location = $"{memberName}:{lineNumber}";
                
                Diagnostics.DiagnosticsManager.DumpObject(obj, $"{objectName}@{location}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to debug dump object: {Error}", ex.Message);
            }
            
            return obj;
        }

        /// <summary>
        /// Debug trace method entry with parameters
        /// </summary>
        public static void DebugTrace(object? parameters = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return;

            var location = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:{lineNumber}";
            
            if (parameters != null)
            {
                _logger.LogTrace("TRACE: {Location} with parameters: {@Parameters}", location, parameters);
            }
            else
            {
                _logger.LogTrace("TRACE: {Location}", location);
            }
        }

        /// <summary>
        /// Debug assertion with detailed context
        /// </summary>
        public static void DebugAssert(this bool condition, string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            // In non-development, never throw or break; just return.
            if (!PowNetConfiguration.IsDevelopment)
                return;

            if (condition)
                return;

            var location = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:{lineNumber}";
            var assertMessage = $"ASSERTION FAILED at {location}: {message}";

            _logger.LogError(assertMessage);

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            else
            {
                throw new PowNetException(assertMessage)
                    .AddParam("Location", location)
                    .AddParam("AssertionMessage", message);
            }
        }

        #endregion

        #region Performance Monitoring Extensions

        /// <summary>
        /// Monitor memory usage around an operation
        /// </summary>
        public static T MonitorMemory<T>(this Func<T> func, string operationName,
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return func();

            var location = $"{memberName}:{lineNumber}";
            var memoryBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);
            var gen1Before = GC.CollectionCount(1);
            var gen2Before = GC.CollectionCount(2);

            try
            {
                var result = func();
                
                var memoryAfter = GC.GetTotalMemory(false);
                var gen0After = GC.CollectionCount(0);
                var gen1After = GC.CollectionCount(1);
                var gen2After = GC.CollectionCount(2);
                
                var memoryDelta = memoryAfter - memoryBefore;
                var gen0Delta = gen0After - gen0Before;
                var gen1Delta = gen1After - gen1Before;
                var gen2Delta = gen2After - gen2Before;

                _logger.LogDebug("Memory monitoring for {Operation} at {Location}: " +
                    "MemoryDelta={MemoryDelta}, GC0={GC0}, GC1={GC1}, GC2={GC2}",
                    operationName, location, memoryDelta, gen0Delta, gen1Delta, gen2Delta);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("Memory monitoring failed for {Operation} at {Location}: {Error}",
                    operationName, location, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Benchmark operation and compare with baseline
        /// </summary>
        public static T Benchmark<T>(this Func<T> func, string operationName, 
            TimeSpan? expectedDuration = null,
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return func();

            var location = $"{memberName}:{lineNumber}";
            var sw = Stopwatch.StartNew();
            
            try
            {
                var result = func();
                sw.Stop();
                
                var duration = sw.Elapsed;
                var status = "OK";
                
                if (expectedDuration.HasValue)
                {
                    if (duration > expectedDuration.Value)
                    {
                        status = "SLOW";
                        var slowdownFactor = duration.TotalMilliseconds / expectedDuration.Value.TotalMilliseconds;
                        _logger.LogWarning("Benchmark {Operation} at {Location} is {Factor:F1}x slower than expected: " +
                            "{ActualDuration}ms vs {ExpectedDuration}ms",
                            operationName, location, slowdownFactor, duration.TotalMilliseconds, expectedDuration.Value.TotalMilliseconds);
                    }
                    else if (duration < TimeSpan.FromTicks(expectedDuration.Value.Ticks / 2))
                    {
                        status = "FAST";
                        _logger.LogDebug("Benchmark {Operation} at {Location} is faster than expected: " +
                            "{ActualDuration}ms vs {ExpectedDuration}ms",
                            operationName, location, duration.TotalMilliseconds, expectedDuration.Value.TotalMilliseconds);
                    }
                }
                
                _logger.LogDebug("Benchmark {Operation} at {Location}: {Duration}ms ({Status})",
                    operationName, location, duration.TotalMilliseconds, status);
                
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError("Benchmark {Operation} at {Location} failed after {Duration}ms: {Error}",
                    operationName, location, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        #endregion

        #region Data Validation Extensions

        /// <summary>
        /// Validate and trace object state
        /// </summary>
        public static T ValidateAndTrace<T>(this T obj, Func<T, bool> validator, string validationName,
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return obj;

            var location = $"{memberName}:{lineNumber}";
            
            try
            {
                var isValid = validator(obj);
                if (!isValid)
                {
                    var errorMessage = $"Validation '{validationName}' failed at {location}";
                    _logger.LogError(errorMessage);
                    
                    if (Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }
                    
                    throw new PowNetValidationException(errorMessage, validationName, obj)
                        .AddParam("Location", location)
                        .AddParam("ValidationName", validationName);
                }
                
                _logger.LogTrace("Validation '{ValidationName}' passed at {Location}", validationName, location);
                return obj;
            }
            catch (Exception ex) when (!(ex is PowNetValidationException))
            {
                var errorMessage = $"Validation '{validationName}' threw exception at {location}: {ex.Message}";
                _logger.LogError(errorMessage);
                throw new PowNetValidationException(errorMessage, validationName, obj)
                    .AddParam("Location", location)
                    .AddParam("OriginalException", ex.Message);
            }
        }

        /// <summary>
        /// Validate collection state and log statistics
        /// </summary>
        public static IEnumerable<T> ValidateCollection<T>(this IEnumerable<T> collection, 
            string collectionName = "Collection",
            Func<T, bool>? itemValidator = null,
            int? expectedMinCount = null,
            int? expectedMaxCount = null,
            [CallerMemberName] string memberName = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return collection;

            var location = $"{memberName}:{lineNumber}";
            var items = collection.ToList(); // Materialize once
            var count = items.Count;
            
            _logger.LogTrace("Collection '{CollectionName}' at {Location} has {Count} items",
                collectionName, location, count);

            // Validate count constraints
            if (expectedMinCount.HasValue && count < expectedMinCount.Value)
            {
                var errorMessage = $"Collection '{collectionName}' at {location} has {count} items, expected at least {expectedMinCount.Value}";
                _logger.LogWarning(errorMessage);
            }

            if (expectedMaxCount.HasValue && count > expectedMaxCount.Value)
            {
                var errorMessage = $"Collection '{collectionName}' at {location} has {count} items, expected at most {expectedMaxCount.Value}";
                _logger.LogWarning(errorMessage);
            }

            // Validate individual items if validator provided
            if (itemValidator != null)
            {
                var invalidItems = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!itemValidator(items[i]))
                    {
                        invalidItems++;
                        _logger.LogWarning("Item at index {Index} in collection '{CollectionName}' failed validation at {Location}",
                            i, collectionName, location);
                    }
                }

                if (invalidItems > 0)
                {
                    _logger.LogWarning("Collection '{CollectionName}' at {Location} has {InvalidCount} invalid items out of {TotalCount}",
                        collectionName, location, invalidItems, count);
                }
            }

            return items;
        }

        #endregion

        #region Exception Context Extensions

        /// <summary>
        /// Add debug context to exception
        /// </summary>
        public static Exception AddDebugContext(this Exception exception,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!PowNetConfiguration.IsDevelopment)
                return exception;

            var location = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:{lineNumber}";
            
            if (exception is PowNetException PowNetEx)
            {
                PowNetEx.AddParam("DebugLocation", location);
                PowNetEx.AddParam("DebugTimestamp", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                
                // Add call stack if not already present
                if (PowNetEx.GetParam<string>("CallStack") == null)
                {
                    var callStack = Diagnostics.DiagnosticsManager.GetCallStack();
                    PowNetEx.AddParam("CallStack", callStack.Frames.Take(5).Select(f => f.MethodName));
                }
            }
            else
            {
                // Add to Data dictionary for regular exceptions
                exception.Data["DebugLocation"] = location;
                exception.Data["DebugTimestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }

            return exception;
        }

        /// <summary>
        /// Log exception with full debug context
        /// </summary>
        public static void LogWithDebugContext(this Exception exception, string? additionalMessage = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var location = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:{lineNumber}";
            
            var message = string.IsNullOrEmpty(additionalMessage) 
                ? $"Exception at {location}: {exception.Message}"
                : $"{additionalMessage} at {location}: {exception.Message}";

            _logger.LogException(exception.AddDebugContext(memberName, filePath, lineNumber), message);
        }

        #endregion

        #region Conditional Debugging

        /// <summary>
        /// Execute action only when debugger is attached
        /// </summary>
        public static void WhenDebuggerAttached(Action action)
        {
            if (Debugger.IsAttached && PowNetConfiguration.IsDevelopment)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Debugger-only action failed");
                }
            }
        }

        /// <summary>
        /// Execute action only in development environment
        /// </summary>
        public static void WhenDevelopment(Action action)
        {
            if (PowNetConfiguration.IsDevelopment)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Development-only action failed");
                }
            }
        }

        /// <summary>
        /// Get debug-safe string representation of object
        /// </summary>
        public static string ToDebugString(this object? obj, int maxLength = 1000)
        {
            if (obj == null) return "null";
            
            try
            {
                string result;
                if (obj is string str)
                {
                    result = $"\"{str}\"";
                }
                else if (obj.GetType().IsPrimitive || obj is decimal || obj is DateTime || obj is TimeSpan || obj is Guid)
                {
                    result = obj.ToString() ?? "null";
                }
                else
                {
                    // For complex objects, try JSON serialization
                    try
                    {
                        result = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions 
                        { 
                            WriteIndented = false,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        });
                    }
                    catch
                    {
                        result = obj.ToString() ?? obj.GetType().Name;
                    }
                }

                return result.Length > maxLength ? result[..maxLength] + "..." : result;
            }
            catch
            {
                return $"<{obj.GetType().Name}>";
            }
        }

        #endregion
    }

    #region Debug Attribute Extensions

    /// <summary>
    /// Attribute to mark methods for automatic profiling
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class ProfileAttribute : Attribute
    {
        public string? Category { get; set; }
        public bool LogParameters { get; set; } = false;
        public bool LogResult { get; set; } = false;
        public TimeSpan? ExpectedDuration { get; set; }
    }

    /// <summary>
    /// Attribute to mark methods for memory monitoring
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class MonitorMemoryAttribute : Attribute
    {
        public long? ExpectedMemoryUsage { get; set; }
        public bool FailOnExcessive { get; set; } = false;
    }

    /// <summary>
    /// Attribute for conditional compilation in debug builds
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property)]
    public class DebugOnlyAttribute : Attribute
    {
        public string? Reason { get; set; }
    }

    #endregion
}