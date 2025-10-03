using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PowNet.Configuration;
using PowNet.Common;

namespace PowNet.Logging
{
    /// <summary>
    /// Advanced logging framework for PowNet with structured logging and performance optimization
    /// </summary>
    public static class PowNetLogger
    {
        #region Logger Configuration

        private static readonly ConcurrentDictionary<string, Logger> _loggers = new();
        private static readonly List<ILogTarget> _logTargets = new();
        private static readonly object _lock = new();
        private static LogLevel _globalLogLevel = LogLevel.Information;
        private static bool _isEnabled = true;

        /// <summary>
        /// Get logger instance for specific category
        /// </summary>
        public static Logger GetLogger(string category)
        {
            return _loggers.GetOrAdd(category, cat => new Logger(cat));
        }

        /// <summary>
        /// Get logger instance for type
        /// </summary>
        public static Logger GetLogger<T>()
        {
            return GetLogger(typeof(T).Name);
        }

        /// <summary>
        /// Add log target
        /// </summary>
        public static void AddTarget(ILogTarget target)
        {
            lock (_lock)
            {
                _logTargets.Add(target);
            }
        }

        /// <summary>
        /// Set global log level
        /// </summary>
        public static void SetGlobalLogLevel(LogLevel level)
        {
            _globalLogLevel = level;
        }

        /// <summary>
        /// Enable/disable logging
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// Initialize default logging configuration
        /// </summary>
        public static void Initialize()
        {
            // Add console target for development
            if (PowNetConfiguration.IsDevelopment)
            {
                AddTarget(new ConsoleLogTarget());
                SetGlobalLogLevel(LogLevel.Debug);
            }

            // Add file target if enabled
            if (PowNetConfiguration.EnableFileLogging)
            {
                AddTarget(new FileLogTarget(PowNetConfiguration.LogsPath));
            }

            // Add structured target for production
            if (PowNetConfiguration.IsProduction)
            {
                AddTarget(new StructuredLogTarget());
                SetGlobalLogLevel(LogLevel.Warning);
            }

            // Configure based on environment
            ConfigureForEnvironment();
        }

        #endregion

        #region Internal Logging

        internal static void WriteLog(LogEntry entry)
        {
            if (!_isEnabled || entry.Level < _globalLogLevel)
                return;

            var targets = _logTargets.ToList(); // Snapshot for thread safety

            foreach (var target in targets)
            {
                try
                {
                    target.WriteLog(entry);
                }
                catch (Exception ex)
                {
                    // Prevent logging errors from crashing the application
                    Debug.WriteLine($"Log target error: {ex.Message}");
                }
            }
        }

        private static void ConfigureForEnvironment()
        {
            var configuredLevel = Enum.TryParse<LogLevel>(PowNetConfiguration.LogLevel, out var level) 
                ? level 
                : LogLevel.Information;
            
            SetGlobalLogLevel(configuredLevel);
        }

        #endregion
    }

    #region Logger Class

    /// <summary>
    /// Logger instance for specific category
    /// </summary>
    public class Logger
    {
        private readonly string _category;

        internal Logger(string category)
        {
            _category = category;
        }

        #region Standard Logging Methods

        public void LogTrace(string message, params object[] args)
        {
            Log(LogLevel.Trace, message, args);
        }

        public void LogDebug(string message, params object[] args)
        {
            Log(LogLevel.Debug, message, args);
        }

        public void LogInformation(string message, params object[] args)
        {
            Log(LogLevel.Information, message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            Log(LogLevel.Warning, message, args);
        }

        public void LogError(string message, params object[] args)
        {
            Log(LogLevel.Error, message, args);
        }

        public void LogCritical(string message, params object[] args)
        {
            Log(LogLevel.Critical, message, args);
        }

        public void LogException(Exception exception, string? message = null, params object[] args)
        {
            var entry = CreateLogEntry(LogLevel.Error, message ?? "Exception occurred", args);
            entry.Exception = exception;
            entry.Properties["ExceptionType"] = exception.GetType().Name;
            entry.Properties["StackTrace"] = exception.StackTrace ?? "";
            
            PowNetLogger.WriteLog(entry);
        }

        #endregion

        #region Structured Logging

        public void LogStructured(LogLevel level, string messageTemplate, object? data = null)
        {
            var entry = CreateLogEntry(level, messageTemplate);
            
            if (data != null)
            {
                if (data is Dictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        entry.Properties[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    // Serialize object properties
                    var properties = data.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        try
                        {
                            var value = prop.GetValue(data);
                            entry.Properties[prop.Name] = value ?? "null";
                        }
                        catch
                        {
                            entry.Properties[prop.Name] = "error_reading_value";
                        }
                    }
                }
            }

            PowNetLogger.WriteLog(entry);
        }

        #endregion

        #region Performance Logging

        public IDisposable BeginScope(string name, object? data = null)
        {
            return new LogScope(this, name, data);
        }

        public void LogPerformance(string operation, TimeSpan duration, object? data = null)
        {
            var entry = CreateLogEntry(LogLevel.Information, $"Performance: {operation}");
            entry.Properties["Operation"] = operation;
            entry.Properties["DurationMs"] = duration.TotalMilliseconds;
            entry.Properties["DurationTicks"] = duration.Ticks;
            
            if (data != null)
            {
                entry.Properties["Data"] = data;
            }

            PowNetLogger.WriteLog(entry);
        }

        public T MeasurePerformance<T>(string operation, Func<T> func, object? data = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = func();
                sw.Stop();
                LogPerformance(operation, sw.Elapsed, data);
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogException(ex, $"Performance measurement failed for operation: {operation}");
                throw;
            }
        }

        public async Task<T> MeasurePerformanceAsync<T>(string operation, Func<Task<T>> func, object? data = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await func();
                sw.Stop();
                LogPerformance(operation, sw.Elapsed, data);
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogException(ex, $"Async performance measurement failed for operation: {operation}");
                throw;
            }
        }

        #endregion

        #region Conditional Logging

        public void LogIf(bool condition, LogLevel level, string message, params object[] args)
        {
            if (condition)
            {
                Log(level, message, args);
            }
        }

        public void LogDebugIf(bool condition, string message, params object[] args)
        {
            LogIf(condition, LogLevel.Debug, message, args);
        }

        public void LogWarningIf(bool condition, string message, params object[] args)
        {
            LogIf(condition, LogLevel.Warning, message, args);
        }

        #endregion

        #region Private Methods

        private void Log(LogLevel level, string message, params object[] args)
        {
            var entry = CreateLogEntry(level, message, args);
            PowNetLogger.WriteLog(entry);
        }

        private static string SafeFormat(string message, object[] args)
        {
            if (args == null || args.Length == 0)
                return message;

            try
            {
                // Try standard string.Format first
                return string.Format(message, args);
            }
            catch (FormatException)
            {
                // Fallback: append args as keyless list to avoid crashing on named template placeholders
                // e.g., message: "Hello {Name}", args: ["Alice"] => "Hello {Name} | args: Alice"
                var renderedArgs = string.Join(", ", args.Select(a => a?.ToString() ?? "null"));
                return $"{message} | args: {renderedArgs}";
            }
        }

        private LogEntry CreateLogEntry(LogLevel level, string message, params object[] args)
        {
            return new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = _category,
                Message = SafeFormat(message, args),
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                ProcessId = Environment.ProcessId,
                Properties = new Dictionary<string, object>()
            };
        }

        #endregion
    }

    #endregion

    #region Log Scope

    internal class LogScope : IDisposable
    {
        private readonly Logger _logger;
        private readonly string _name;
        private readonly object? _data;
        private readonly Stopwatch _stopwatch;

        public LogScope(Logger logger, string name, object? data)
        {
            _logger = logger;
            _name = name;
            _data = data;
            _stopwatch = Stopwatch.StartNew();
            
            _logger.LogDebug("Scope started: {ScopeName}", _name);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.LogDebug("Scope completed: {ScopeName} in {Duration}ms", _name, _stopwatch.ElapsedMilliseconds);
        }
    }

    #endregion

    #region Log Targets

    /// <summary>
    /// Interface for log targets
    /// </summary>
    public interface ILogTarget
    {
        void WriteLog(LogEntry entry);
    }

    /// <summary>
    /// Console log target with colored output
    /// </summary>
    public class ConsoleLogTarget : ILogTarget
    {
        private static readonly Dictionary<LogLevel, ConsoleColor> Colors = new()
        {
            [LogLevel.Trace] = ConsoleColor.Gray,
            [LogLevel.Debug] = ConsoleColor.Cyan,
            [LogLevel.Information] = ConsoleColor.Green,
            [LogLevel.Warning] = ConsoleColor.Yellow,
            [LogLevel.Error] = ConsoleColor.Red,
            [LogLevel.Critical] = ConsoleColor.Magenta
        };

        public void WriteLog(LogEntry entry)
        {
            var originalColor = Console.ForegroundColor;
            
            try
            {
                if (Colors.TryGetValue(entry.Level, out var color))
                {
                    Console.ForegroundColor = color;
                }

                var message = FormatMessage(entry);
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        private string FormatMessage(LogEntry entry)
        {
            var sb = new StringBuilder();
            sb.Append($"[{entry.Timestamp:HH:mm:ss.fff}] ");
            sb.Append($"[{entry.Level.ToString().ToUpper()}] ");
            sb.Append($"[{entry.Category}] ");
            sb.Append(entry.Message);

            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append($"Exception: {entry.Exception}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// File log target with rotation
    /// </summary>
    public class FileLogTarget : ILogTarget
    {
        private readonly string _logDirectory;
        private readonly object _writeLock = new();
        private string? _currentLogFile;
        private DateTime _currentLogDate;

        public FileLogTarget(string logDirectory)
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);
        }

        public void WriteLog(LogEntry entry)
        {
            lock (_writeLock)
            {
                EnsureLogFile(entry.Timestamp);
                
                var message = FormatMessage(entry);
                File.AppendAllText(_currentLogFile!, message + Environment.NewLine);
                
                // Check if file needs rotation
                CheckFileRotation();
            }
        }

        private void EnsureLogFile(DateTime timestamp)
        {
            var logDate = timestamp.Date;
            
            if (_currentLogFile == null || _currentLogDate != logDate)
            {
                _currentLogDate = logDate;
                _currentLogFile = Path.Combine(_logDirectory, $"PowNet-{logDate:yyyyMMdd}.log");
            }
        }

        private void CheckFileRotation()
        {
            if (_currentLogFile != null && File.Exists(_currentLogFile))
            {
                var fileInfo = new FileInfo(_currentLogFile);
                if (fileInfo.Length > PowNetConfiguration.MaxLogFileSizeBytes)
                {
                    var timestamp = DateTime.Now.ToString("HHmmss");
                    var rotatedFile = _currentLogFile.Replace(".log", $"-{timestamp}.log");
                    File.Move(_currentLogFile, rotatedFile);
                }
            }
        }

        private string FormatMessage(LogEntry entry)
        {
            var json = JsonSerializer.Serialize(new
            {
                timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = entry.Level.ToString(),
                category = entry.Category,
                message = entry.Message,
                threadId = entry.ThreadId,
                processId = entry.ProcessId,
                properties = entry.Properties,
                exception = entry.Exception?.ToString()
            });
            
            return json;
        }
    }

    /// <summary>
    /// Structured log target for production environments
    /// </summary>
    public class StructuredLogTarget : ILogTarget
    {
        public void WriteLog(LogEntry entry)
        {
            // This could be connected to external logging systems like ELK, Splunk, etc.
            var structuredLog = new
            {
                timestamp = entry.Timestamp,
                level = entry.Level.ToString(),
                logger = entry.Category,
                message = entry.Message,
                thread = entry.ThreadId,
                process = entry.ProcessId,
                properties = entry.Properties,
                exception = entry.Exception != null ? new
                {
                    type = entry.Exception.GetType().Name,
                    message = entry.Exception.Message,
                    stackTrace = entry.Exception.StackTrace
                } : null,
                environment = PowNetConfiguration.Environment,
                application = "PowNet"
            };

            // For now, write to debug output
            var json = JsonSerializer.Serialize(structuredLog, new JsonSerializerOptions { WriteIndented = false });
            Debug.WriteLine(json);
        }
    }

    #endregion

    #region Supporting Classes

    /// <summary>
    /// Log levels
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Log entry container
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ThreadId { get; set; }
        public int ProcessId { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    #endregion

    #region Extension Methods

    /// <summary>
    /// Logging extensions for common scenarios
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Log method entry with parameters
        /// </summary>
        public static void LogMethodEntry(this Logger logger, object? parameters = null, 
            [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            logger.LogTrace("Entering method: {MethodName}", methodName);
            
            if (parameters != null)
            {
                logger.LogStructured(LogLevel.Trace, "Method parameters for {MethodName}", 
                    new { MethodName = methodName, Parameters = parameters });
            }
        }

        /// <summary>
        /// Log method exit with result
        /// </summary>
        public static void LogMethodExit(this Logger logger, object? result = null,
            [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            logger.LogTrace("Exiting method: {MethodName}", methodName);
            
            if (result != null)
            {
                logger.LogStructured(LogLevel.Trace, "Method result for {MethodName}",
                    new { MethodName = methodName, Result = result });
            }
        }

        /// <summary>
        /// Log HTTP request information
        /// </summary>
        public static void LogHttpRequest(this Logger logger, string method, string path, 
            int statusCode, TimeSpan duration, object? additionalData = null)
        {
            logger.LogStructured(LogLevel.Information, "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms",
                new
                {
                    Method = method,
                    Path = path,
                    StatusCode = statusCode,
                    Duration = duration.TotalMilliseconds,
                    AdditionalData = additionalData
                });
        }

        /// <summary>
        /// Log database operation
        /// </summary>
        public static void LogDatabaseOperation(this Logger logger, string operation, string? query = null, 
            TimeSpan? duration = null, int? affectedRows = null)
        {
            logger.LogStructured(LogLevel.Debug, "Database operation: {Operation}",
                new
                {
                    Operation = operation,
                    Query = query,
                    Duration = duration?.TotalMilliseconds,
                    AffectedRows = affectedRows
                });
        }

        /// <summary>
        /// Log business operation
        /// </summary>
        public static void LogBusinessOperation(this Logger logger, string operation, bool success, 
            object? data = null, string? errorMessage = null)
        {
            var level = success ? LogLevel.Information : LogLevel.Warning;
            
            logger.LogStructured(level, "Business operation: {Operation} - {Result}",
                new
                {
                    Operation = operation,
                    Success = success,
                    Result = success ? "SUCCESS" : "FAILED",
                    Data = data,
                    ErrorMessage = errorMessage
                });
        }
    }

    #endregion
}