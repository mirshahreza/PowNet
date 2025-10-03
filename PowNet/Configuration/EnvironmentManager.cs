using System.Collections.Concurrent;
using PowNet.Common;

namespace PowNet.Configuration
{
    /// <summary>
    /// Environment-specific configuration management for PowNet framework
    /// </summary>
    public static class EnvironmentManager
    {
        #region Environment Detection

        private static readonly ConcurrentDictionary<string, EnvironmentInfo> _environments = new();
        private static EnvironmentInfo? _currentEnvironment;
        private static readonly object _lock = new();

        /// <summary>
        /// Get current environment information
        /// </summary>
        public static EnvironmentInfo CurrentEnvironment
        {
            get
            {
                if (_currentEnvironment == null)
                {
                    lock (_lock)
                    {
                        _currentEnvironment ??= DetectEnvironment();
                    }
                }
                return _currentEnvironment;
            }
        }

        /// <summary>
        /// Register a new environment configuration
        /// </summary>
        public static void RegisterEnvironment(EnvironmentInfo environmentInfo)
        {
            _environments.TryAdd(environmentInfo.Name.ToLowerInvariant(), environmentInfo);
        }

        /// <summary>
        /// Get environment by name
        /// </summary>
        public static EnvironmentInfo? GetEnvironment(string name)
        {
            _environments.TryGetValue(name.ToLowerInvariant(), out var env);
            return env;
        }

        /// <summary>
        /// Get all registered environments
        /// </summary>
        public static IEnumerable<EnvironmentInfo> GetAllEnvironments()
        {
            return _environments.Values;
        }

        #endregion

        #region Environment-Specific Configuration

        /// <summary>
        /// Get configuration value specific to current environment
        /// </summary>
        public static T GetEnvironmentValue<T>(string key, T defaultValue = default!)
        {
            var envSpecificKey = $"{CurrentEnvironment.Name}:{key}";
            return PowNetConfiguration.GetConfigValue(envSpecificKey, 
                PowNetConfiguration.GetConfigValue(key, defaultValue));
        }

        /// <summary>
        /// Set environment-specific configuration value
        /// </summary>
        public static void SetEnvironmentValue(string key, object value)
        {
            var envSpecificKey = $"{CurrentEnvironment.Name}:{key}";
            PowNetConfiguration.SetConfigValue(envSpecificKey, value);
        }

        /// <summary>
        /// Load environment-specific configuration file
        /// </summary>
        public static void LoadEnvironmentConfig(string environmentName)
        {
            var configFileName = $"appsettings.{environmentName}.json";
            
            if (!File.Exists(configFileName))
            {
                throw new PowNetConfigurationException($"Environment configuration file not found: {configFileName}")
                    .AddParam("EnvironmentName", environmentName)
                    .AddParam("ConfigFileName", configFileName);
            }

            try
            {
                // This would typically merge with base configuration
                // For now, we'll just refresh the main configuration
                PowNetConfiguration.RefreshSettings();
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException($"Failed to load environment configuration for {environmentName}")
                    .AddParam("EnvironmentName", environmentName)
                    .AddParam("Error", ex.Message);
            }
        }

        #endregion

        #region Cloud Environment Detection

        /// <summary>
        /// Detect if running in Azure
        /// </summary>
        public static bool IsRunningInAzure()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"));
        }

        /// <summary>
        /// Detect if running in AWS
        /// </summary>
        public static bool IsRunningInAWS()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_REGION")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
        }

        /// <summary>
        /// Detect if running in Google Cloud
        /// </summary>
        public static bool IsRunningInGoogleCloud()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAE_APPLICATION"));
        }

        /// <summary>
        /// Detect if running in Docker
        /// </summary>
        public static bool IsRunningInDocker()
        {
            return File.Exists("/.dockerenv") ||
                   Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        }

        /// <summary>
        /// Detect if running in Kubernetes
        /// </summary>
        public static bool IsRunningInKubernetes()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
        }

        #endregion

        #region Environment Validation

        /// <summary>
        /// Validate current environment configuration
        /// </summary>
        public static EnvironmentValidationResult ValidateEnvironment()
        {
            var result = new EnvironmentValidationResult
            {
                EnvironmentName = CurrentEnvironment.Name
            };

            try
            {
                // Validate environment-specific requirements
                ValidateEnvironmentRequirements(result);

                // Validate cloud configuration if applicable
                ValidateCloudConfiguration(result);

                // Validate security settings for environment
                ValidateEnvironmentSecurity(result);

                result.IsValid = !result.Issues.Any(i => i.Severity == IssueSeverity.Error);
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Message = $"Environment validation failed: {ex.Message}",
                    Key = "ValidationError"
                });
                result.IsValid = false;
            }

            return result;
        }

        #endregion

        #region Private Helper Methods

        private static EnvironmentInfo DetectEnvironment()
        {
            var envName = PowNetConfiguration.Environment;
            
            var info = new EnvironmentInfo
            {
                Name = envName,
                IsProduction = envName.Equals("Production", StringComparison.OrdinalIgnoreCase),
                IsDevelopment = envName.Equals("Development", StringComparison.OrdinalIgnoreCase),
                IsStaging = envName.Equals("Staging", StringComparison.OrdinalIgnoreCase),
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OperatingSystem = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                IsCloud = IsRunningInAzure() || IsRunningInAWS() || IsRunningInGoogleCloud(),
                IsContainer = IsRunningInDocker(),
                IsKubernetes = IsRunningInKubernetes()
            };

            // Detect cloud provider
            if (IsRunningInAzure())
                info.CloudProvider = CloudProvider.Azure;
            else if (IsRunningInAWS())
                info.CloudProvider = CloudProvider.AWS;
            else if (IsRunningInGoogleCloud())
                info.CloudProvider = CloudProvider.GoogleCloud;

            // Register predefined environments
            RegisterPredefinedEnvironments();

            return info;
        }

        private static void RegisterPredefinedEnvironments()
        {
            // Development Environment
            RegisterEnvironment(new EnvironmentInfo
            {
                Name = "Development",
                IsDevelopment = true,
                RequiredFeatures = ["DebugLogging", "HotReload", "DeveloperExceptionPage"],
                RecommendedSettings = new Dictionary<string, object>
                {
                    ["Logging:LogLevel:Default"] = "Debug",
                    ["PowNet:Security:RequireHttps"] = false,
                    ["PowNet:Performance:EnableCaching"] = false
                }
            });

            // Staging Environment
            RegisterEnvironment(new EnvironmentInfo
            {
                Name = "Staging",
                IsStaging = true,
                RequiredFeatures = ["ProductionLogging", "PerformanceMonitoring"],
                RecommendedSettings = new Dictionary<string, object>
                {
                    ["Logging:LogLevel:Default"] = "Information",
                    ["PowNet:Security:RequireHttps"] = true,
                    ["PowNet:Performance:EnableCaching"] = true
                }
            });

            // Production Environment
            RegisterEnvironment(new EnvironmentInfo
            {
                Name = "Production",
                IsProduction = true,
                RequiredFeatures = ["SecureLogging", "PerformanceMonitoring", "HealthChecks", "Telemetry"],
                RecommendedSettings = new Dictionary<string, object>
                {
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["PowNet:Security:RequireHttps"] = true,
                    ["PowNet:Performance:EnableCaching"] = true,
                    ["PowNet:Security:MaxLoginAttempts"] = 3
                }
            });
        }

        private static void ValidateEnvironmentRequirements(EnvironmentValidationResult result)
        {
            var envInfo = GetEnvironment(CurrentEnvironment.Name);
            if (envInfo?.RequiredFeatures != null)
            {
                foreach (var feature in envInfo.RequiredFeatures)
                {
                    // This would check if required features are properly configured
                    // For example, check if logging is configured for production
                    ValidateFeature(feature, result);
                }
            }

            if (envInfo?.RecommendedSettings != null)
            {
                foreach (var setting in envInfo.RecommendedSettings)
                {
                    var currentValue = PowNetConfiguration.GetConfigValue<object>(setting.Key);
                    if (!Equals(currentValue, setting.Value))
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Message = $"Recommended setting '{setting.Key}' should be '{setting.Value}' but is '{currentValue}'",
                            Key = setting.Key
                        });
                    }
                }
            }
        }

        private static void ValidateFeature(string feature, EnvironmentValidationResult result)
        {
            switch (feature.ToLowerInvariant())
            {
                case "debuglogging":
                    if (PowNetConfiguration.LogLevel != "Debug")
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Message = "Debug logging is not enabled for development environment",
                            Key = "Logging:LogLevel:Default"
                        });
                    }
                    break;

                case "productionlogging":
                    if (!PowNetConfiguration.EnableFileLogging)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Error,
                            Message = "File logging must be enabled for production environments",
                            Key = "PowNet:Logging:EnableFileLogging"
                        });
                    }
                    break;

                case "securehttps":
                    if (!PowNetConfiguration.RequireHttps)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Error,
                            Message = "HTTPS must be required for production environments",
                            Key = "PowNet:Security:RequireHttps"
                        });
                    }
                    break;
            }
        }

        private static void ValidateCloudConfiguration(EnvironmentValidationResult result)
        {
            if (CurrentEnvironment.IsCloud)
            {
                // Validate cloud-specific configurations
                switch (CurrentEnvironment.CloudProvider)
                {
                    case CloudProvider.Azure:
                        ValidateAzureConfiguration(result);
                        break;
                    case CloudProvider.AWS:
                        ValidateAWSConfiguration(result);
                        break;
                    case CloudProvider.GoogleCloud:
                        ValidateGoogleCloudConfiguration(result);
                        break;
                }
            }
        }

        private static void ValidateAzureConfiguration(EnvironmentValidationResult result)
        {
            var requiredEnvVars = new[] { "WEBSITE_SITE_NAME", "AZURE_CLIENT_ID" };
            foreach (var envVar in requiredEnvVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Message = $"Azure environment variable '{envVar}' is not set",
                        Key = envVar
                    });
                }
            }
        }

        private static void ValidateAWSConfiguration(EnvironmentValidationResult result)
        {
            var requiredEnvVars = new[] { "AWS_REGION" };
            foreach (var envVar in requiredEnvVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Message = $"AWS environment variable '{envVar}' is not set",
                        Key = envVar
                    });
                }
            }
        }

        private static void ValidateGoogleCloudConfiguration(EnvironmentValidationResult result)
        {
            var requiredEnvVars = new[] { "GOOGLE_CLOUD_PROJECT" };
            foreach (var envVar in requiredEnvVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Message = $"Google Cloud environment variable '{envVar}' is not set",
                        Key = envVar
                    });
                }
            }
        }

        private static void ValidateEnvironmentSecurity(EnvironmentValidationResult result)
        {
            if (CurrentEnvironment.IsProduction)
            {
                // Production-specific security checks
                if (PowNetConfiguration.EncryptionSecret == "PowNet-Default-Secret-Key")
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Message = "Default encryption secret must not be used in production",
                        Key = "PowNet:EncryptionSecret"
                    });
                }

                if (!PowNetConfiguration.RequireHttps)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Message = "HTTPS must be required in production environment",
                        Key = "PowNet:Security:RequireHttps"
                    });
                }
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Environment information container
    /// </summary>
    public class EnvironmentInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsProduction { get; set; }
        public bool IsDevelopment { get; set; }
        public bool IsStaging { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public bool IsCloud { get; set; }
        public bool IsContainer { get; set; }
        public bool IsKubernetes { get; set; }
        public CloudProvider? CloudProvider { get; set; }
        public List<string> RequiredFeatures { get; set; } = new();
        public Dictionary<string, object> RecommendedSettings { get; set; } = new();
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    }

    /// <summary>
    /// Cloud provider enumeration
    /// </summary>
    public enum CloudProvider
    {
        Azure,
        AWS,
        GoogleCloud,
        Other
    }

    /// <summary>
    /// Environment validation result
    /// </summary>
    public class EnvironmentValidationResult
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public bool IsValid { get; set; } = true;
        public List<ValidationIssue> Issues { get; set; } = new();

        public IEnumerable<ValidationIssue> Errors => Issues.Where(i => i.Severity == IssueSeverity.Error);
        public IEnumerable<ValidationIssue> Warnings => Issues.Where(i => i.Severity == IssueSeverity.Warning);
        public IEnumerable<ValidationIssue> Info => Issues.Where(i => i.Severity == IssueSeverity.Info);

        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                var errorMessages = Errors.Select(e => e.Message);
                var message = $"Environment validation failed for '{EnvironmentName}':\n{string.Join("\n", errorMessages)}";
                
                throw new PowNetConfigurationException(message)
                    .AddParam("EnvironmentName", EnvironmentName)
                    .AddParam("ErrorCount", Errors.Count())
                    .AddParam("WarningCount", Warnings.Count());
            }
        }
    }

    /// <summary>
    /// Validation issue container
    /// </summary>
    public class ValidationIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string? RecommendedAction { get; set; }
    }

    /// <summary>
    /// Issue severity levels
    /// </summary>
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    #endregion
}