using System.Text.Json;
using System.Text;
using PowNet.Configuration;
using PowNet.Common;
using PowNet.Logging;
using Microsoft.Extensions.Configuration; // Added for merged Additional methods

namespace PowNet.Extensions
{
    /// <summary>
    /// Advanced configuration management extensions for PowNet framework
    /// </summary>
    public static class ConfigurationExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("Configuration");

        #region Additional (Merged)
        /// <summary>
        /// Get required configuration value or throw if missing.
        /// </summary>
        public static T GetRequired<T>(this IConfiguration cfg, string key)
        {
            var val = cfg.GetValue<T>(key);
            if (EqualityComparer<T>.Default.Equals(val, default!))
                throw new InvalidOperationException($"Missing required configuration key: '{key}'.");
            return val!;
        }

        /// <summary>
        /// Bind section or return provided default instance.
        /// </summary>
        public static T BindOrDefault<T>(this IConfiguration cfg, string section, T @default) where T : notnull
        {
            var obj = @default;
            cfg.GetSection(section).Bind(obj);
            return obj;
        }

        /// <summary>
        /// Observe changes on a key with throttle (fire-and-forget callback).
        /// </summary>
        public static IDisposable OnChangeAsync(this IConfiguration cfg, string key, Func<string, Task> handler, TimeSpan throttle)
        {
            var locker = new object();
            DateTime last = DateTime.MinValue;
            string? lastVal = cfg[key];
            var token = cfg.GetReloadToken();
            var reg = token.RegisterChangeCallback(async _ =>
            {
                lock (locker)
                {
                    if (DateTime.UtcNow - last < throttle) return;
                    last = DateTime.UtcNow;
                }
                var current = cfg[key] ?? string.Empty;
                if (!string.Equals(current, lastVal, StringComparison.Ordinal))
                {
                    lastVal = current;
                    await handler(current);
                }
            }, null);
            return reg;
        }
        #endregion

        #region Configuration Binding Extensions

        /// <summary>
        /// Bind configuration section to strongly typed object
        /// </summary>
        public static T BindConfiguration<T>(this string sectionKey, T? defaultValue = default) where T : new()
        {
            try
            {
                var section = PowNetConfiguration.PowNetSection[sectionKey];
                if (section == null)
                {
                    _logger.LogWarning("Configuration section '{SectionKey}' not found, using default value", sectionKey);
                    return defaultValue ?? new T();
                }

                var json = section.ToJsonString();
                var result = JsonSerializer.Deserialize<T>(json);
                return result ?? defaultValue ?? new T();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to bind configuration section '{SectionKey}'", sectionKey);
                return defaultValue ?? new T();
            }
        }

        /// <summary>
        /// Bind configuration with validation
        /// </summary>
        public static T BindAndValidateConfiguration<T>(this string sectionKey, 
            Func<T, ValidationResult>? validator = null,
            T? defaultValue = default) where T : new()
        {
            var config = sectionKey.BindConfiguration(defaultValue);
            
            if (validator != null)
            {
                var validationResult = validator(config);
                if (!validationResult.IsValid)
                {
                    throw new PowNetConfigurationException($"Configuration validation failed for section '{sectionKey}'")
                        .AddParam("SectionKey", sectionKey)
                        .AddParam("ValidationErrors", validationResult.Issues);
                }
            }

            return config;
        }

        /// <summary>
        /// Get configuration value with transformation
        /// </summary>
        public static TResult GetConfigValue<T, TResult>(this string key, 
            Func<T, TResult> transformer, 
            TResult defaultValue = default!)
        {
            try
            {
                var value = PowNetConfiguration.GetConfigValue<T>(key);
                return value != null ? transformer(value) : defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to transform configuration value for key '{Key}'", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Get configuration list from delimited string
        /// </summary>
        public static List<T> GetConfigList<T>(this string key, 
            char delimiter = ',', 
            Func<string, T>? converter = null)
        {
            var value = PowNetConfiguration.GetConfigValue<string>(key);
            if (string.IsNullOrEmpty(value))
                return new List<T>();

            try
            {
                var items = value.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .Where(s => !string.IsNullOrEmpty(s));

                if (converter != null)
                {
                    return items.Select(converter).ToList();
                }

                return items.Cast<T>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to parse configuration list for key '{Key}'", key);
                return new List<T>();
            }
        }

        /// <summary>
        /// Get configuration dictionary from JSON
        /// </summary>
        public static Dictionary<string, T> GetConfigDictionary<T>(this string key)
        {
            try
            {
                var value = PowNetConfiguration.GetConfigValue<string>(key);
                if (string.IsNullOrEmpty(value))
                    return new Dictionary<string, T>();

                return JsonSerializer.Deserialize<Dictionary<string, T>>(value) ?? new Dictionary<string, T>();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to parse configuration dictionary for key '{Key}'", key);
                return new Dictionary<string, T>();
            }
        }

        #endregion

        #region Configuration Templates

        /// <summary>
        /// Generate configuration template for a type
        /// </summary>
        public static string GenerateConfigurationTemplate<T>(string sectionName = "", bool includeComments = true) where T : new()
        {
            var instance = new T();
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            var json = new StringBuilder();
            
            if (includeComments)
            {
                json.AppendLine($"// Configuration template for {typeof(T).Name}");
                json.AppendLine($"// Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                json.AppendLine();
            }

            json.AppendLine("{");
            
            if (!string.IsNullOrEmpty(sectionName))
            {
                json.AppendLine($"  \"{sectionName}\": {{");
            }

            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                var value = prop.GetValue(instance);
                var indent = string.IsNullOrEmpty(sectionName) ? "  " : "    ";

                if (includeComments)
                {
                    json.AppendLine($"{indent}// {prop.PropertyType.Name} property");
                }

                var jsonValue = JsonSerializer.Serialize(value);
                json.Append($"{indent}\"{prop.Name}\": {jsonValue}");
                
                if (i < properties.Length - 1)
                    json.Append(",");
                
                json.AppendLine();
            }

            if (!string.IsNullOrEmpty(sectionName))
            {
                json.AppendLine("  }");
            }
            
            json.AppendLine("}");

            return json.ToString();
        }

        /// <summary>
        /// Create configuration documentation
        /// </summary>
        public static string GenerateConfigurationDocumentation<T>(string sectionName = "") where T : new()
        {
            var type = typeof(T);
            var properties = type.GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            var doc = new StringBuilder();
            doc.AppendLine($"# Configuration Documentation: {type.Name}");
            doc.AppendLine();
            
            if (!string.IsNullOrEmpty(sectionName))
            {
                doc.AppendLine($"**Section:** `{sectionName}`");
                doc.AppendLine();
            }

            doc.AppendLine("## Properties");
            doc.AppendLine();

            foreach (var prop in properties)
            {
                doc.AppendLine($"### {prop.Name}");
                doc.AppendLine($"**Type:** `{GetFriendlyTypeName(prop.PropertyType)}`");
                
                var defaultValue = GetDefaultValue(prop.PropertyType);
                if (defaultValue != null)
                {
                    doc.AppendLine($"**Default:** `{defaultValue}`");
                }

                // Try to get XML documentation or attribute description
                var description = GetPropertyDescription(prop);
                if (!string.IsNullOrEmpty(description))
                {
                    doc.AppendLine($"**Description:** {description}");
                }

                doc.AppendLine();
            }

            return doc.ToString();
        }

        #endregion

        #region Configuration Validation

        /// <summary>
        /// Validate configuration section with custom rules
        /// </summary>
        public static ConfigurationValidationResult ValidateConfigurationSection<T>(
            this string sectionKey,
            Func<T, IEnumerable<string>>? customValidator = null) where T : new()
        {
            var result = new ConfigurationValidationResult();

            try
            {
                var config = sectionKey.BindConfiguration<T>();
                
                // Basic validation using data annotations
                var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(config);
                
                if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(config, validationContext, validationResults, true))
                {
                    result.Errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Validation error"));
                }

                // Custom validation
                if (customValidator != null)
                {
                    var customErrors = customValidator(config);
                    result.Errors.AddRange(customErrors);
                }

                result.IsValid = !result.Errors.Any();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configuration validation failed: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// Validate all configuration sections
        /// </summary>
        public static Dictionary<string, ConfigurationValidationResult> ValidateAllConfigurationSections()
        {
            var results = new Dictionary<string, ConfigurationValidationResult>();

            // Get all configuration sections
            var sections = GetAllConfigurationSections();
            
            foreach (var section in sections)
            {
                try
                {
                    var result = new ConfigurationValidationResult();
                    
                    // Basic existence check
                    if (PowNetConfiguration.PowNetSection[section] == null)
                    {
                        result.Warnings.Add($"Section '{section}' is referenced but not found in configuration");
                    }
                    else
                    {
                        result.IsValid = true;
                    }

                    results[section] = result;
                }
                catch (Exception ex)
                {
                    results[section] = new ConfigurationValidationResult
                    {
                        IsValid = false,
                        Errors = { $"Validation failed: {ex.Message}" }
                    };
                }
            }

            return results;
        }

        #endregion

        #region Configuration Comparison

        /// <summary>
        /// Compare two configuration objects and return differences
        /// </summary>
        public static ConfigurationDifference CompareConfigurations<T>(T current, T other, string? name = null)
        {
            var differences = new ConfigurationDifference
            {
                ConfigurationName = name ?? typeof(T).Name
            };

            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead)
                .ToArray();

            foreach (var prop in properties)
            {
                var currentValue = prop.GetValue(current);
                var otherValue = prop.GetValue(other);

                if (!Equals(currentValue, otherValue))
                {
                    differences.Changes.Add(new ConfigurationChange
                    {
                        PropertyName = prop.Name,
                        CurrentValue = currentValue,
                        NewValue = otherValue,
                        PropertyType = prop.PropertyType.Name
                    });
                }
            }

            return differences;
        }

        /// <summary>
        /// Generate configuration change report
        /// </summary>
        public static string GenerateChangeReport(ConfigurationDifference difference)
        {
            var report = new StringBuilder();
            report.AppendLine($"Configuration Change Report: {difference.ConfigurationName}");
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.AppendLine($"Changes: {difference.Changes.Count}");
            report.AppendLine();

            if (!difference.Changes.Any())
            {
                report.AppendLine("No changes detected.");
                return report.ToString();
            }

            foreach (var change in difference.Changes)
            {
                report.AppendLine($"Property: {change.PropertyName} ({change.PropertyType})");
                report.AppendLine($"  Current: {change.CurrentValue ?? "null"}");
                report.AppendLine($"  New:     {change.NewValue ?? "null"}");
                report.AppendLine();
            }

            return report.ToString();
        }

        #endregion

        #region Configuration Migration

        /// <summary>
        /// Migrate configuration from old format to new format
        /// </summary>
        public static T MigrateConfiguration<TOld, T>(TOld oldConfig, Func<TOld, T> migrator) where T : new()
        {
            try
            {
                _logger.LogInformation("Migrating configuration from {OldType} to {NewType}", 
                    typeof(TOld).Name, typeof(T).Name);

                var newConfig = migrator(oldConfig);
                
                _logger.LogInformation("Configuration migration completed successfully");
                return newConfig;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Configuration migration failed");
                throw new PowNetConfigurationException("Configuration migration failed")
                    .AddParam("OldType", typeof(TOld).Name)
                    .AddParam("NewType", typeof(T).Name)
                    .AddParam("Error", ex.Message);
            }
        }

        /// <summary>
        /// Create configuration backup with metadata
        /// </summary>
        public static ConfigurationBackup CreateDetailedBackup()
        {
            return new ConfigurationBackup
            {
                BackupId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                Environment = PowNetConfiguration.Environment,
                BackupPath = PowNetConfiguration.CreateConfigurationBackup(),
                ConfigurationSnapshot = CaptureConfigurationSnapshot(),
                Metadata = new Dictionary<string, object>
                {
                    ["MachineName"] = Environment.MachineName,
                    ["UserName"] = Environment.UserName,
                    ["ProcessId"] = Environment.ProcessId,
                    ["WorkingDirectory"] = Environment.CurrentDirectory
                }
            };
        }

        #endregion

        #region Private Helper Methods

        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return GetFriendlyTypeName(Nullable.GetUnderlyingType(type)!) + "?";
            }

            return type.Name switch
            {
                "String" => "string",
                "Int32" => "int",
                "Int64" => "long",
                "Boolean" => "bool",
                "Double" => "double",
                "Decimal" => "decimal",
                "DateTime" => "DateTime",
                "TimeSpan" => "TimeSpan",
                _ => type.Name
            };
        }

        private static object? GetDefaultValue(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);
            
            return null;
        }

        private static string? GetPropertyDescription(System.Reflection.PropertyInfo property)
        {
            // Try to get description from DisplayAttribute or DescriptionAttribute
            var displayAttr = property.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), false)
                .FirstOrDefault() as System.ComponentModel.DisplayNameAttribute;
            if (displayAttr != null)
                return displayAttr.DisplayName;

            var descAttr = property.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                .FirstOrDefault() as System.ComponentModel.DescriptionAttribute;
            if (descAttr != null)
                return descAttr.Description;

            return null;
        }

        private static List<string> GetAllConfigurationSections()
        {
            var sections = new List<string>();
            
            try
            {
                var rootSection = PowNetConfiguration.PowNetSection;
                foreach (var kvp in rootSection.AsObject())
                {
                    sections.Add(kvp.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to enumerate configuration sections");
            }

            return sections;
        }

        private static Dictionary<string, object> CaptureConfigurationSnapshot()
        {
            var snapshot = new Dictionary<string, object>();

            try
            {
                // Capture key configuration values
                snapshot["Environment"] = PowNetConfiguration.Environment;
                snapshot["LogLevel"] = PowNetConfiguration.LogLevel;
                snapshot["WorkspacePath"] = PowNetConfiguration.WorkspacePath;
                snapshot["EncryptionSecretHash"] = PowNetConfiguration.EncryptionSecret.ComputeSHA256();
                snapshot["DatabaseTimeout"] = PowNetConfiguration.DefaultCommandTimeout;
                snapshot["CacheExpiration"] = PowNetConfiguration.DefaultCacheExpirationMinutes;
                snapshot["MaxConcurrentRequests"] = PowNetConfiguration.MaxConcurrentRequests;
                snapshot["RequireHttps"] = PowNetConfiguration.RequireHttps;
                snapshot["EnableFileLogging"] = PowNetConfiguration.EnableFileLogging;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to capture configuration snapshot");
            }

            return snapshot;
        }

        #endregion
    }

    #region Supporting Classes

    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ConfigurationDifference
    {
        public string ConfigurationName { get; set; } = string.Empty;
        public List<ConfigurationChange> Changes { get; set; } = new();
        public DateTime ComparisonTime { get; set; } = DateTime.UtcNow;
    }

    public class ConfigurationChange
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? CurrentValue { get; set; }
        public object? NewValue { get; set; }
        public string PropertyType { get; set; } = string.Empty;
    }

    public class ConfigurationBackup
    {
        public Guid BackupId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Environment { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public Dictionary<string, object> ConfigurationSnapshot { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    #endregion
}