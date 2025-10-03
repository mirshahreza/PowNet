using System.Reflection;
using System.Runtime.CompilerServices;
using PowNet.Extensions;

namespace PowNet.Common
{
    /// <summary>
    /// Custom exception class for PowNet framework with enhanced metadata support
    /// </summary>
    public class PowNetException : Exception
    {
        private readonly List<KeyValuePair<string, object>> _errorMetadata = [];
        
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
        public string? SourceMethod { get; }
        public string? SourceFile { get; }
        public int SourceLine { get; }

        public PowNetException(string message, MethodBase? methodBase = null, 
            [CallerMemberName] string? callerMember = null,
            [CallerFilePath] string? callerFile = null,
            [CallerLineNumber] int callerLine = 0) : base(message)
        {
            SourceMethod = callerMember ?? methodBase?.Name;
            SourceFile = callerFile;
            SourceLine = callerLine;
            
            if (methodBase != null)
            {
                AddParam("Site", methodBase.GetPlaceInfo());
            }
            
            AddParam("Timestamp", OccurredAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            if (!string.IsNullOrEmpty(SourceMethod))
                AddParam("Method", SourceMethod);
            if (!string.IsNullOrEmpty(SourceFile))
                AddParam("File", Path.GetFileName(SourceFile));
            if (SourceLine > 0)
                AddParam("Line", SourceLine);
        }

        public PowNetException(string message, Exception innerException, MethodBase? methodBase = null,
            [CallerMemberName] string? callerMember = null,
            [CallerFilePath] string? callerFile = null,
            [CallerLineNumber] int callerLine = 0) : base(message, innerException)
        {
            SourceMethod = callerMember ?? methodBase?.Name;
            SourceFile = callerFile;
            SourceLine = callerLine;
            
            if (methodBase != null)
            {
                AddParam("Site", methodBase.GetPlaceInfo());
            }
            
            AddParam("Timestamp", OccurredAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            if (!string.IsNullOrEmpty(SourceMethod))
                AddParam("Method", SourceMethod);
            if (!string.IsNullOrEmpty(SourceFile))
                AddParam("File", Path.GetFileName(SourceFile));
            if (SourceLine > 0)
                AddParam("Line", SourceLine);
        }

        /// <summary>
        /// Adds a parameter to the exception metadata
        /// </summary>
        public PowNetException AddParam(string name, object? value)
        {
            if (!string.IsNullOrWhiteSpace(name) && value != null)
            {
                _errorMetadata.Add(new KeyValuePair<string, object>(name, value));
            }
            return this;
        }

        /// <summary>
        /// Gets all parameters as read-only collection
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, object>> GetParams()
        {
            return _errorMetadata.AsReadOnly();
        }

        /// <summary>
        /// Gets formatted metadata string
        /// </summary>
        public string GetMetadata()
        {
            return string.Join(", ", _errorMetadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        /// <summary>
        /// Gets a specific parameter value by name
        /// </summary>
        public T? GetParam<T>(string name)
        {
            var param = _errorMetadata.FirstOrDefault(kvp => kvp.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            return param.Value is T value ? value : default;
        }

        /// <summary>
        /// Creates a detailed error report
        /// </summary>
        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"PowNet Exception: {Message}");
            report.AppendLine($"Occurred At: {OccurredAt:yyyy-MM-dd HH:mm:ss UTC}");
            
            if (!string.IsNullOrEmpty(SourceMethod))
                report.AppendLine($"Method: {SourceMethod}");
            if (!string.IsNullOrEmpty(SourceFile))
                report.AppendLine($"File: {Path.GetFileName(SourceFile)}");
            if (SourceLine > 0)
                report.AppendLine($"Line: {SourceLine}");
            
            if (_errorMetadata.Count > 0)
            {
                report.AppendLine("Metadata:");
                foreach (var param in _errorMetadata)
                {
                    report.AppendLine($"  {param.Key}: {param.Value}");
                }
            }
            
            if (InnerException != null)
            {
                report.AppendLine($"Inner Exception: {InnerException.Message}");
            }
            
            return report.ToString();
        }
    }

    /// <summary>
    /// Specific exception types for different scenarios
    /// </summary>
    public class PowNetValidationException : PowNetException
    {
        public string? PropertyName { get; }
        public object? AttemptedValue { get; }

        public PowNetValidationException(string message, string? propertyName = null, object? attemptedValue = null, 
            MethodBase? methodBase = null, [CallerMemberName] string? callerMember = null,
            [CallerFilePath] string? callerFile = null, [CallerLineNumber] int callerLine = 0) 
            : base(message, methodBase, callerMember, callerFile, callerLine)
        {
            PropertyName = propertyName;
            AttemptedValue = attemptedValue;
            
            if (!string.IsNullOrEmpty(propertyName))
                AddParam("Property", propertyName);
            if (attemptedValue != null)
                AddParam("AttemptedValue", attemptedValue);
        }
    }

    public class PowNetConfigurationException : PowNetException
    {
        public string? ConfigurationKey { get; }

        public PowNetConfigurationException(string message, string? configurationKey = null, 
            MethodBase? methodBase = null, [CallerMemberName] string? callerMember = null,
            [CallerFilePath] string? callerFile = null, [CallerLineNumber] int callerLine = 0) 
            : base(message, methodBase, callerMember, callerFile, callerLine)
        {
            ConfigurationKey = configurationKey;
            
            if (!string.IsNullOrEmpty(configurationKey))
                AddParam("ConfigurationKey", configurationKey);
        }
    }

    public class PowNetSecurityException : PowNetException
    {
        public string? SecurityContext { get; }

        public PowNetSecurityException(string message, string? securityContext = null, 
            MethodBase? methodBase = null, [CallerMemberName] string? callerMember = null,
            [CallerFilePath] string? callerFile = null, [CallerLineNumber] int callerLine = 0) 
            : base(message, methodBase, callerMember, callerFile, callerLine)
        {
            SecurityContext = securityContext;
            
            if (!string.IsNullOrEmpty(securityContext))
                AddParam("SecurityContext", securityContext);
        }
    }

    /// <summary>
    /// Extension methods for PowNetException
    /// </summary>
    public static class PowNetExceptionExtensions
    {
        /// <summary>
        /// Converts PowNetException to standard Exception with all metadata preserved
        /// </summary>
        public static Exception GetEx(this PowNetException PowNetException)
        {
            var ex = new Exception(PowNetException.Message, PowNetException.InnerException);
            
            // Add all metadata to Data dictionary
            foreach (var param in PowNetException.GetParams())
            {
                if (!ex.Data.Contains(param.Key))
                {
                    ex.Data.Add(param.Key, param.Value);
                }
            }
            
            // Add source information
            ex.Data.Add("PowNet_OccurredAt", PowNetException.OccurredAt);
            if (!string.IsNullOrEmpty(PowNetException.SourceMethod))
                ex.Data.Add("PowNet_SourceMethod", PowNetException.SourceMethod);
            if (!string.IsNullOrEmpty(PowNetException.SourceFile))
                ex.Data.Add("PowNet_SourceFile", PowNetException.SourceFile);
            if (PowNetException.SourceLine > 0)
                ex.Data.Add("PowNet_SourceLine", PowNetException.SourceLine);
            
            return ex;
        }

        /// <summary>
        /// Logs the exception with all metadata (requires ILogger)
        /// </summary>
        public static void LogError(this PowNetException exception, Action<string>? logger = null)
        {
            var detailedReport = exception.GetDetailedReport();
            logger?.Invoke(detailedReport);
            // Could also write to System.Diagnostics.Debug or Console as fallback
            System.Diagnostics.Debug.WriteLine(detailedReport);
        }
    }

    #region Backward Compatibility
    /// <summary>
    /// Backward compatibility alias - will be marked as obsolete in future versions
    /// </summary>
    [Obsolete("Use PowNetException instead. This alias will be removed in a future version.")]
    public class AppEndException : PowNetException
    {
        public AppEndException(string message, MethodBase? methodBase = null) : base(message, methodBase) { }
        public AppEndException(string message, Exception innerException, MethodBase? methodBase = null) : base(message, innerException, methodBase) { }
    }

    [Obsolete("Use PowNetExceptionExtensions instead. This alias will be removed in a future version.")]
    public static class AppEndExceptionExtensions
    {
        public static Exception GetEx(this AppEndException appEndException) => ((PowNetException)appEndException).GetEx();
    }
    #endregion
}