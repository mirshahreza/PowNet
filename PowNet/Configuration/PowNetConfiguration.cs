using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using PowNet.Common;

namespace PowNet.Configuration
{
    /// <summary>
    /// Enhanced configuration management for PowNet framework with environment support
    /// </summary>
    public static class PowNetConfiguration
    {
        #region Environment Management

        private static string? _currentEnvironment;
        private static readonly object _environmentLock = new();

        /// <summary>
        /// Current application environment
        /// </summary>
        public static string Environment
        {
            get
            {
                if (_currentEnvironment == null)
                {
                    lock (_environmentLock)
                    {
                        _currentEnvironment ??= DetermineEnvironment();
                    }
                }
                return _currentEnvironment;
            }
            set
            {
                lock (_environmentLock)
                {
                    _currentEnvironment = value;
                    RefreshSettings(); // Refresh settings when environment changes
                }
            }
        }

        /// <summary>
        /// Check if running in development environment
        /// </summary>
        public static bool IsDevelopment => Environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Check if running in production environment
        /// </summary>
        public static bool IsProduction => Environment.Equals("Production", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Check if running in staging environment
        /// </summary>
        public static bool IsStaging => Environment.Equals("Staging", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Path Configuration

        public static string WorkspacePath => GetConfigValue("PowNet:WorkspacePath", "workspace");

        public static string ServerObjectsPath => Path.Combine(WorkspacePath, "server");

        public static string ApiCallsPath => Path.Combine(WorkspacePath, "apicalls");

        public static string ClientObjectsPath => Path.Combine(WorkspacePath, "client");
        
        public static string PowNetPackagesPath => Path.Combine(WorkspacePath, "PowNetpackages");
        
        public static string PowNetPlugins => Path.Combine(WorkspacePath, "plugins");

        public static string LogsPath => GetConfigValue("PowNet:LogsPath", Path.Combine(WorkspacePath, "logs"));

        public static string TempPath => GetConfigValue("PowNet:TempPath", Path.Combine(WorkspacePath, "temp"));

        public static string BackupPath => GetConfigValue("PowNet:BackupPath", Path.Combine(WorkspacePath, "backup"));

        #endregion

        #region Core Configuration

        public static string SectionName = "PowNet";

        public static string EncryptionSecret => GetSecretValue("PowNet:EncryptionSecret", "PowNet-Default-Secret-Key");
        
        public static string RootUserName => GetConfigValue("PowNet:RootUserName", "admin");
        
        public static string RootRoleName => GetConfigValue("PowNet:RootRoleName", "Administrator");

        public static JsonNode PowNetSection => AppSettings[SectionName] ?? new JsonObject();

        public static DirectoryInfo ProjectRoot => new(".");

        #endregion

        #region Database Configuration

        public static int DefaultCommandTimeout => GetConfigValue("PowNet:Database:CommandTimeout", 30);

        public static int MaxRetryAttempts => GetConfigValue("PowNet:Database:MaxRetryAttempts", 3);

        public static bool EnableConnectionPooling => GetConfigValue("PowNet:Database:EnableConnectionPooling", true);

        #endregion

        #region Security Configuration

        public static int JwtExpirationHours => GetConfigValue("PowNet:Security:JwtExpirationHours", 24);

        public static int PasswordMinLength => GetConfigValue("PowNet:Security:PasswordMinLength", 8);

        public static int MaxLoginAttempts => GetConfigValue("PowNet:Security:MaxLoginAttempts", 5);

        public static int LockoutDurationMinutes => GetConfigValue("PowNet:Security:LockoutDurationMinutes", 30);

        public static bool RequireHttps => GetConfigValue("PowNet:Security:RequireHttps", IsProduction);

        #endregion

        #region Performance Configuration

        public static int DefaultCacheExpirationMinutes => GetConfigValue("PowNet:Performance:DefaultCacheExpirationMinutes", 30);

        public static int MaxConcurrentRequests => GetConfigValue("PowNet:Performance:MaxConcurrentRequests", System.Environment.ProcessorCount * 10);

        public static int RequestTimeoutSeconds => GetConfigValue("PowNet:Performance:RequestTimeoutSeconds", 30);

        #endregion

        #region Logging Configuration

        public static string LogLevel => GetConfigValue("Logging:LogLevel:Default", IsDevelopment ? "Debug" : "Information");

        public static bool EnableFileLogging => GetConfigValue("PowNet:Logging:EnableFileLogging", true);

        public static int LogRetentionDays => GetConfigValue("PowNet:Logging:RetentionDays", 30);

        public static long MaxLogFileSizeBytes => GetConfigValue("PowNet:Logging:MaxFileSizeBytes", 10 * 1024 * 1024); // 10MB

        #endregion

        #region Settings Management

        private static JsonNode? _appsettings;
        private static readonly object _settingsLock = new();
        private static readonly ConcurrentDictionary<string, object> _configCache = new();
        private static readonly ConcurrentDictionary<string, object> _runtimeOverrides = new(); // runtime set values override env vars
        private static DateTime _lastConfigRefresh = DateTime.MinValue;
        private static readonly TimeSpan _configCacheDuration = TimeSpan.FromMinutes(5);

        public static JsonNode AppSettings
        {
            get
            {
                if (_appsettings == null || ShouldRefreshConfig())
                {
                    lock (_settingsLock)
                    {
                        if (_appsettings == null || ShouldRefreshConfig())
                        {
                            LoadAppSettings();
                        }
                    }
                }
                return _appsettings!;
            }
        }

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Get configuration value with type conversion and default fallback
        /// </summary>
        public static T GetConfigValue<T>(string key, T defaultValue = default!)
        {
            try
            {
                // 1. Runtime override always wins
                if (_runtimeOverrides.TryGetValue(key, out var overrideVal))
                {
                    if (overrideVal is T tv) return tv;
                    return ConvertValue<T>(overrideVal?.ToString());
                }

                // Check cache next
                var cacheKey = $"config:{key}:{typeof(T).Name}";
                if (_configCache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is T cached)
                {
                    return cached;
                }

                // Environment variables (only if no runtime override)
                var envKey = key.Replace(":", "__");
                var envValue = System.Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrEmpty(envValue))
                {
                    var convertedEnvValue = ConvertValue<T>(envValue);
                    _configCache[cacheKey] = convertedEnvValue!;
                    return convertedEnvValue;
                }

                // Appsettings
                var value = GetNestedValue(AppSettings, key);
                if value != null)
                {
                    var convertedValue = ConvertValue<T>(value.ToString());
                    _configCache[cacheKey] = convertedValue!;
                    return convertedValue;
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                LogConfigurationError($"Error getting config value for key '{key}'", ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Get secret value with enhanced security handling
        /// </summary>
        public static string GetSecretValue(string key, string defaultValue = "")
        {
            try
            {
                if (_runtimeOverrides.TryGetValue(key, out var runtime) && runtime is string rs)
                    return rs;

                var envKey = key.Replace(":", "__");
                var envValue = System.Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrEmpty(envValue))
                {
                    return envValue;
                }

                return GetConfigValue(key, defaultValue);
            }
            catch (Exception ex)
            {
                LogConfigurationError($"Error getting secret value for key '{key}'", ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Set configuration value at runtime (takes precedence over env vars for process lifetime)
        /// </summary>
        public static void SetConfigValue(string key, object value)
        {
            try
            {
                var keys = key.Split(':');
                var current = AppSettings;

                for (int i = 0; i < keys.Length - 1; i++)
                {
                    if (current[keys[i]] == null)
                    {
                        current[keys[i]] = new JsonObject();
                    }
                    current = current[keys[i]]!;
                }

                current[keys.Last()] = JsonValue.Create(value);

                // store runtime override
                _runtimeOverrides[key] = value;

                // Clear cached reads for this key exact-prefix
                var cacheKeys = _configCache.Keys.Where(k => k.StartsWith($"config:{key}:", StringComparison.Ordinal)).ToList();
                foreach (var cacheKey in cacheKeys)
                {
                    _configCache.TryRemove(cacheKey, out _);
                }
            }
            catch (Exception ex)
            {
                LogConfigurationError($"Error setting config value for key '{key}'", ex);
                throw new PowNetConfigurationException($"Failed to set configuration value for key '{key}'", key)
                    .AddParam("Value", value)
                    .AddParam("Error", ex.Message);
            }
        }

        /// <summary>
        /// Validate all configuration values
        /// </summary>
        public static ConfigurationValidationResult ValidateConfiguration()
        {
            var result = new ConfigurationValidationResult();

            try
            {
                // Validate required paths
                ValidatePathConfiguration(result);

                // Validate connection strings
                ValidateConnectionStrings(result);

                // Validate security settings
                ValidateSecuritySettings(result);

                // Validate performance settings
                ValidatePerformanceSettings(result);

                result.IsValid = !result.Errors.Any();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configuration validation failed: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        public static string GetConnectionStringByName(string connectionName)
        {
            try
            {
                var envSpecificKey = $"ConnectionStrings__{Environment}__{connectionName}";
                var envSpecificValue = System.Environment.GetEnvironmentVariable(envSpecificKey);
                if (!string.IsNullOrEmpty(envSpecificValue))
                {
                    return envSpecificValue;
                }

                var envKey = $"ConnectionStrings__{connectionName}";
                var envValue = System.Environment.GetEnvironmentVariable(envKey);
                if (!string.IsNullOrEmpty(envValue))
                {
                    return envValue;
                }

                if (AppSettings["ConnectionStrings"]?[connectionName] is null)
                {
                    throw new PowNetConfigurationException($"Connection string '{connectionName}' not found", connectionName);
                }

                return AppSettings["ConnectionStrings"]![connectionName]!.ToString();
            }
            catch (PowNetConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException($"Error retrieving connection string '{connectionName}'", connectionName)
                    .AddParam("Error", ex.Message);
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> GetConnectionStrings()
        {
            var connectionStrings = AppSettings["ConnectionStrings"]; 
            if (connectionStrings == null)
            {
                throw new PowNetConfigurationException("ConnectionStrings section not found in configuration");
            }

            foreach (var kvp in connectionStrings.AsObject())
            {
                if (kvp.Value != null)
                {
                    yield return new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString());
                }
            }
        }

        public static void Save()
        {
            try
            {
                lock (_settingsLock)
                {
                    var appSettingsText = JsonSerializer.Serialize(AppSettings, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    
                    var configPath = GetConfigurationFilePath();
                    var dir = Path.GetDirectoryName(configPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(configPath, appSettingsText);
                    
                    RefreshSettings();
                    LogConfigurationChange("Configuration", "Saved to file");
                }
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException("Failed to save configuration")
                    .AddParam("Error", ex.Message);
            }
        }

        public static void RefreshSettings()
        {
            lock (_settingsLock)
            {
                _appsettings = null;
                _configCache.Clear();
                _lastConfigRefresh = DateTime.UtcNow;
            }
        }

        public static string CreateConfigurationBackup()
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"appsettings_backup_{timestamp}.json";
                var backupPath = Path.Combine(BackupPath, backupFileName);

                Directory.CreateDirectory(BackupPath);
                
                var configPath = GetConfigurationFilePath();
                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath, "{ }");
                }
                File.Copy(configPath, backupPath, true);

                return backupPath;
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException("Failed to create configuration backup")
                    .AddParam("Error", ex.Message);
            }
        }

        public static void RestoreConfigurationFromBackup(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException($"Backup file not found: {backupPath}");
                }

                var configPath = GetConfigurationFilePath();
                File.Copy(backupPath, configPath, true);
                
                RefreshSettings();
                LogConfigurationChange("Configuration", $"Restored from backup: {backupPath}");
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException("Failed to restore configuration from backup")
                    .AddParam("BackupPath", backupPath)
                    .AddParam("Error", ex.Message);
            }
        }

        #endregion

        #region Private Helper Methods

        private static string DetermineEnvironment()
        {
            var env = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                     System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                     System.Environment.GetEnvironmentVariable("PowNet_ENVIRONMENT");

            if (!string.IsNullOrEmpty(env))
            {
                return env;
            }

            try
            {
                var baseConfigPath = "appsettings.json";
                if (File.Exists(baseConfigPath))
                {
                    var configText = File.ReadAllText(baseConfigPath);
                    var node = JsonNode.Parse(configText);
                    var configEnv = node?["Environment"]?.ToString();
                    if (!string.IsNullOrEmpty(configEnv))
                    {
                        return configEnv;
                    }
                }
            }
            catch { }

            return "Production";
        }

        private static void LoadAppSettings()
        {
            var configPath = GetConfigurationFilePath();
            
            if (!File.Exists(configPath))
            {
                _appsettings = new JsonObject();
                _lastConfigRefresh = DateTime.UtcNow;
                return;
            }

            try
            {
                var configText = File.ReadAllText(configPath);
                _appsettings = JsonNode.Parse(configText) ?? new JsonObject();
                _lastConfigRefresh = DateTime.UtcNow;
            }
            catch (JsonException ex)
            {
                throw new PowNetConfigurationException("Failed to parse configuration file")
                    .AddParam("ConfigPath", configPath)
                    .AddParam("JsonError", ex.Message);
            }
        }

        private static string GetConfigurationFilePath()
        {
            var baseFileName = "appsettings.json";

            // Avoid recursion: if environment not determined yet, use base file only
            if (string.IsNullOrEmpty(_currentEnvironment))
            {
                return baseFileName;
            }

            var envFileName = $"appsettings.{Environment}.json";

            if (File.Exists(envFileName))
            {
                return envFileName;
            }

            return baseFileName;
        }

        private static bool ShouldRefreshConfig()
        {
            return DateTime.UtcNow - _lastConfigRefresh > _configCacheDuration;
        }

        private static JsonNode? GetNestedValue(JsonNode node, string key)
        {
            var keys = key.Split(':');
            var current = node;

            foreach (var k in keys)
            {
                if (current?[k] == null)
                {
                    return null;
                }
                current = current[k];
            }

            return current;
        }

        private static T ConvertValue<T>(string? value)
        {
            if (value == null)
            {
                return default!;
            }

            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(string))
            {
                return (T)(object)value;
            }

            if (underlyingType == typeof(bool))
            {
                return (T)(object)bool.Parse(value);
            }

            if (underlyingType == typeof(int))
            {
                return (T)(object)int.Parse(value);
            }

            if (underlyingType == typeof(long))
            {
                return (T)(object)long.Parse(value);
            }

            if (underlyingType == typeof(TimeSpan))
            {
                return (T)(object)TimeSpan.Parse(value);
            }

            return (T)Convert.ChangeType(value, underlyingType)!;
        }

        #endregion

        #region Validation Methods

        private static void ValidatePathConfiguration(ConfigurationValidationResult result)
        {
            var paths = new Dictionary<string, string>
            {
                ["WorkspacePath"] = WorkspacePath,
                ["ServerObjectsPath"] = ServerObjectsPath,
                ["LogsPath"] = LogsPath,
                ["TempPath"] = TempPath,
                ["BackupPath"] = BackupPath
            };

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path.Value))
                {
                    result.Errors.Add($"{path.Key} is not configured");
                    continue;
                }

                try
                {
                    var fullPath = Path.GetFullPath(path.Value);
                    if (!Directory.Exists(fullPath))
                    {
                        result.Warnings.Add($"{path.Key} directory does not exist: {fullPath}");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Invalid path for {path.Key}: {ex.Message}");
                }
            }
        }

        private static void ValidateConnectionStrings(ConfigurationValidationResult result)
        {
            try
            {
                var connectionStrings = GetConnectionStrings().ToList();
                if (!connectionStrings.Any())
                {
                    result.Warnings.Add("No connection strings configured");
                }

                foreach (var cs in connectionStrings)
                {
                    if (string.IsNullOrWhiteSpace(cs.Value))
                    {
                        result.Errors.Add($"Connection string '{cs.Key}' is empty");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error validating connection strings: {ex.Message}");
            }
        }

        private static void ValidateSecuritySettings(ConfigurationValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(EncryptionSecret) || EncryptionSecret == "PowNet-Default-Secret-Key")
            {
                result.Errors.Add("EncryptionSecret must be configured with a secure value");
            }

            if (EncryptionSecret.Length < 16)
            {
                result.Errors.Add("EncryptionSecret must be at least 16 characters long");
            }

            if (PasswordMinLength < 6)
            {
                result.Warnings.Add("PasswordMinLength is less than 6, consider increasing for better security");
            }

            if (IsProduction && !RequireHttps)
            {
                result.Warnings.Add("HTTPS is not required in production environment");
            }
        }

        private static void ValidatePerformanceSettings(ConfigurationValidationResult result)
        {
            if (MaxConcurrentRequests <= 0)
            {
                result.Errors.Add("MaxConcurrentRequests must be greater than 0");
            }

            if (RequestTimeoutSeconds <= 0)
            {
                result.Errors.Add("RequestTimeoutSeconds must be greater than 0");
            }

            if (DefaultCacheExpirationMinutes <= 0)
            {
                result.Warnings.Add("DefaultCacheExpirationMinutes should be greater than 0 for effective caching");
            }
        }

        private static void LogConfigurationError(string message, Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Configuration Error: {message} - {exception}");
        }

        private static void LogConfigurationChange(string key, object value)
        {
            System.Diagnostics.Debug.WriteLine($"Configuration Changed: {key} = {value}");
        }

        #endregion
    }

    #region Supporting Classes

    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                var message = $"Configuration validation failed:\n{string.Join("\n", Errors)}";
                throw new PowNetConfigurationException(message)
                    .AddParam("ErrorCount", Errors.Count)
                    .AddParam("WarningCount", Warnings.Count);
            }
        }
    }

    #endregion
}