using System.Collections.Concurrent;
using PowNet.Common;
using PowNet.Configuration;

namespace PowNet.Features
{
    /// <summary>
    /// Feature flag and toggle management for PowNet framework
    /// </summary>
    public static class FeatureManager
    {
        #region Feature Flag Management

        private static readonly ConcurrentDictionary<string, Feature> _features = new();
        private static readonly List<IFeatureProvider> _providers = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Register a feature flag
        /// </summary>
        public static void RegisterFeature(Feature feature)
        {
            _features.AddOrUpdate(feature.Name, feature, (key, existing) =>
            {
                existing.UpdateFrom(feature);
                return existing;
            });
        }

        /// <summary>
        /// Register multiple features
        /// </summary>
        public static void RegisterFeatures(IEnumerable<Feature> features)
        {
            foreach (var feature in features)
            {
                RegisterFeature(feature);
            }
        }

        /// <summary>
        /// Check if feature is enabled
        /// </summary>
        public static bool IsEnabled(string featureName, FeatureContext? context = null)
        {
            if (!_features.TryGetValue(featureName, out var feature))
            {
                // Feature not registered - check configuration
                return PowNetConfiguration.GetConfigValue($"Features:{featureName}", false);
            }

            return feature.IsEnabled(context ?? FeatureContext.Default);
        }

        /// <summary>
        /// Get feature configuration
        /// </summary>
        public static T? GetFeatureConfig<T>(string featureName, string configKey, T? defaultValue = default)
        {
            if (_features.TryGetValue(featureName, out var feature))
            {
                if (feature.Configuration.TryGetValue(configKey, out var value) && value is T typedValue)
                {
                    return typedValue;
                }
            }

            // Fallback to configuration system
            return PowNetConfiguration.GetConfigValue($"Features:{featureName}:{configKey}", defaultValue);
        }

        /// <summary>
        /// Enable feature for specific context
        /// </summary>
        public static void EnableFeature(string featureName, FeatureContext? context = null)
        {
            var feature = GetOrCreateFeature(featureName);
            feature.Enable(context);
        }

        /// <summary>
        /// Disable feature for specific context
        /// </summary>
        public static void DisableFeature(string featureName, FeatureContext? context = null)
        {
            var feature = GetOrCreateFeature(featureName);
            feature.Disable(context);
        }

        /// <summary>
        /// Get all registered features
        /// </summary>
        public static IEnumerable<Feature> GetAllFeatures()
        {
            return _features.Values.ToList();
        }

        /// <summary>
        /// Get feature by name
        /// </summary>
        public static Feature? GetFeature(string featureName)
        {
            _features.TryGetValue(featureName, out var feature);
            return feature;
        }

        #endregion

        #region Feature Providers

        /// <summary>
        /// Register feature provider
        /// </summary>
        public static void RegisterProvider(IFeatureProvider provider)
        {
            lock (_lock)
            {
                _providers.Add(provider);
                LoadFeaturesFromProvider(provider);
            }
        }

        /// <summary>
        /// Load features from all providers
        /// </summary>
        public static void LoadFeaturesFromProviders()
        {
            lock (_lock)
            {
                foreach (var provider in _providers)
                {
                    LoadFeaturesFromProvider(provider);
                }
            }
        }

        /// <summary>
        /// Refresh features from providers
        /// </summary>
        public static async Task RefreshFeaturesAsync()
        {
            var refreshTasks = _providers.Select(async provider =>
            {
                try
                {
                    if (provider is IAsyncFeatureProvider asyncProvider)
                    {
                        var features = await asyncProvider.GetFeaturesAsync();
                        RegisterFeatures(features);
                    }
                    else
                    {
                        var features = provider.GetFeatures();
                        RegisterFeatures(features);
                    }
                }
                catch (Exception ex)
                {
                    // Log provider refresh error
                    System.Diagnostics.Debug.WriteLine($"Feature provider refresh failed: {ex.Message}");
                }
            });

            await Task.WhenAll(refreshTasks);
        }

        #endregion

        #region Built-in Features

        /// <summary>
        /// Initialize built-in framework features
        /// </summary>
        public static void InitializeBuiltInFeatures()
        {
            // Performance features
            RegisterFeature(new Feature("PerformanceMonitoring")
            {
                Description = "Enable performance monitoring and metrics collection",
                Category = "Performance",
                DefaultEnabled = !PowNetConfiguration.IsDevelopment
            });

            RegisterFeature(new Feature("CacheEnabled")
            {
                Description = "Enable application caching",
                Category = "Performance",
                DefaultEnabled = true,
                Configuration = new Dictionary<string, object>
                {
                    ["DefaultExpirationMinutes"] = PowNetConfiguration.DefaultCacheExpirationMinutes,
                    ["MaxCacheSize"] = "100MB"
                }
            });

            // Security features
            RegisterFeature(new Feature("RateLimiting")
            {
                Description = "Enable rate limiting for API endpoints",
                Category = "Security",
                DefaultEnabled = PowNetConfiguration.IsProduction,
                Configuration = new Dictionary<string, object>
                {
                    ["RequestsPerMinute"] = 60,
                    ["BurstLimit"] = 100
                }
            });

            RegisterFeature(new Feature("EnhancedSecurity")
            {
                Description = "Enable enhanced security headers and validation",
                Category = "Security",
                DefaultEnabled = !PowNetConfiguration.IsDevelopment
            });

            // Logging features
            RegisterFeature(new Feature("DetailedLogging")
            {
                Description = "Enable detailed application logging",
                Category = "Logging",
                DefaultEnabled = PowNetConfiguration.IsDevelopment,
                Rules = new List<FeatureRule>
                {
                    new EnvironmentFeatureRule("Development", true),
                    new EnvironmentFeatureRule("Production", false)
                }
            });

            RegisterFeature(new Feature("AuditLogging")
            {
                Description = "Enable audit trail logging",
                Category = "Security",
                DefaultEnabled = PowNetConfiguration.IsProduction
            });

            // Development features
            RegisterFeature(new Feature("DebugMode")
            {
                Description = "Enable debug mode with enhanced error information",
                Category = "Development",
                DefaultEnabled = PowNetConfiguration.IsDevelopment,
                Rules = new List<FeatureRule>
                {
                    new EnvironmentFeatureRule("Development", true),
                    new EnvironmentFeatureRule("Production", false)
                }
            });

            RegisterFeature(new Feature("HotReload")
            {
                Description = "Enable configuration hot reloading",
                Category = "Development",
                DefaultEnabled = PowNetConfiguration.IsDevelopment
            });

            // API features
            RegisterFeature(new Feature("Swagger")
            {
                Description = "Enable Swagger API documentation",
                Category = "API",
                DefaultEnabled = !PowNetConfiguration.IsProduction
            });

            RegisterFeature(new Feature("ApiVersioning")
            {
                Description = "Enable API versioning support",
                Category = "API",
                DefaultEnabled = true
            });

            // Business features (examples)
            RegisterFeature(new Feature("NewUserInterface")
            {
                Description = "Enable new user interface components",
                Category = "UI",
                DefaultEnabled = false,
                Rules = new List<FeatureRule>
                {
                    new PercentageFeatureRule(10) // Enable for 10% of users
                }
            });
        }

        #endregion

        #region Private Helper Methods

        private static Feature GetOrCreateFeature(string featureName)
        {
            return _features.GetOrAdd(featureName, name => new Feature(name));
        }

        private static void LoadFeaturesFromProvider(IFeatureProvider provider)
        {
            try
            {
                var features = provider.GetFeatures();
                RegisterFeatures(features);
            }
            catch (Exception ex)
            {
                // Log provider loading error
                System.Diagnostics.Debug.WriteLine($"Feature provider loading failed: {ex.Message}");
            }
        }

        #endregion
    }

    #region Core Feature Classes

    /// <summary>
    /// Represents a feature flag with its configuration and rules
    /// </summary>
    public class Feature
    {
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public bool DefaultEnabled { get; set; } = false;
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<FeatureRule> Rules { get; set; } = new();
        public Dictionary<string, bool> ContextOverrides { get; set; } = new();

        public Feature(string name)
        {
            Name = name;
        }

        public bool IsEnabled(FeatureContext context)
        {
            // Check context-specific overrides first
            var contextKey = context.GetContextKey();
            if (ContextOverrides.TryGetValue(contextKey, out var contextEnabled))
            {
                return contextEnabled;
            }

            // Check rules
            foreach (var rule in Rules)
            {
                if (rule.ShouldApply(context))
                {
                    return rule.IsEnabled(context);
                }
            }

            // Check configuration override
            var configKey = $"Features:{Name}";
            if (PowNetConfiguration.GetConfigValue<bool?>(configKey) is bool configValue)
            {
                return configValue;
            }

            return DefaultEnabled;
        }

        public void Enable(FeatureContext? context = null)
        {
            context ??= FeatureContext.Default;
            ContextOverrides[context.GetContextKey()] = true;
        }

        public void Disable(FeatureContext? context = null)
        {
            context ??= FeatureContext.Default;
            ContextOverrides[context.GetContextKey()] = false;
        }

        public void UpdateFrom(Feature other)
        {
            Description = other.Description;
            Category = other.Category;
            DefaultEnabled = other.DefaultEnabled;
            Configuration = other.Configuration;
            Rules = other.Rules;
        }
    }

    /// <summary>
    /// Context for feature evaluation
    /// </summary>
    public class FeatureContext
    {
        public string? UserId { get; set; }
        public string? Environment { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();

        public static FeatureContext Default => new()
        {
            Environment = PowNetConfiguration.Environment
        };

        public string GetContextKey()
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(Environment))
                parts.Add($"env:{Environment}");
            
            if (!string.IsNullOrEmpty(UserId))
                parts.Add($"user:{UserId}");
            
            foreach (var prop in Properties)
            {
                parts.Add($"{prop.Key}:{prop.Value}");
            }

            return string.Join("|", parts);
        }
    }

    #endregion

    #region Feature Rules

    /// <summary>
    /// Base class for feature evaluation rules
    /// </summary>
    public abstract class FeatureRule
    {
        public abstract bool ShouldApply(FeatureContext context);
        public abstract bool IsEnabled(FeatureContext context);
    }

    /// <summary>
    /// Environment-based feature rule
    /// </summary>
    public class EnvironmentFeatureRule : FeatureRule
    {
        public string Environment { get; set; }
        public bool Enabled { get; set; }

        public EnvironmentFeatureRule(string environment, bool enabled)
        {
            Environment = environment;
            Enabled = enabled;
        }

        public override bool ShouldApply(FeatureContext context)
        {
            return string.Equals(context.Environment, Environment, StringComparison.OrdinalIgnoreCase);
        }

        public override bool IsEnabled(FeatureContext context)
        {
            return Enabled;
        }
    }

    /// <summary>
    /// User-based feature rule
    /// </summary>
    public class UserFeatureRule : FeatureRule
    {
        public HashSet<string> UserIds { get; set; }
        public bool Enabled { get; set; }

        public UserFeatureRule(IEnumerable<string> userIds, bool enabled)
        {
            UserIds = new HashSet<string>(userIds, StringComparer.OrdinalIgnoreCase);
            Enabled = enabled;
        }

        public override bool ShouldApply(FeatureContext context)
        {
            return !string.IsNullOrEmpty(context.UserId) && UserIds.Contains(context.UserId);
        }

        public override bool IsEnabled(FeatureContext context)
        {
            return Enabled;
        }
    }

    /// <summary>
    /// Percentage-based feature rule (gradual rollout)
    /// </summary>
    public class PercentageFeatureRule : FeatureRule
    {
        public double Percentage { get; set; }

        public PercentageFeatureRule(double percentage)
        {
            Percentage = Math.Max(0, Math.Min(100, percentage));
        }

        public override bool ShouldApply(FeatureContext context)
        {
            return true; // Always apply percentage rule
        }

        public override bool IsEnabled(FeatureContext context)
        {
            // Use consistent hash of context for deterministic results
            var contextHash = context.GetContextKey().GetHashCode();
            var normalized = Math.Abs(contextHash % 100);
            return normalized < Percentage;
        }
    }

    /// <summary>
    /// Time-based feature rule
    /// </summary>
    public class TimeWindowFeatureRule : FeatureRule
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Enabled { get; set; }

        public TimeWindowFeatureRule(DateTime startTime, DateTime endTime, bool enabled)
        {
            StartTime = startTime;
            EndTime = endTime;
            Enabled = enabled;
        }

        public override bool ShouldApply(FeatureContext context)
        {
            var now = DateTime.UtcNow;
            return now >= StartTime && now <= EndTime;
        }

        public override bool IsEnabled(FeatureContext context)
        {
            return Enabled;
        }
    }

    #endregion

    #region Feature Providers

    /// <summary>
    /// Interface for feature providers
    /// </summary>
    public interface IFeatureProvider
    {
        IEnumerable<Feature> GetFeatures();
    }

    /// <summary>
    /// Interface for async feature providers
    /// </summary>
    public interface IAsyncFeatureProvider : IFeatureProvider
    {
        Task<IEnumerable<Feature>> GetFeaturesAsync();
    }

    /// <summary>
    /// Configuration-based feature provider
    /// </summary>
    public class ConfigurationFeatureProvider : IFeatureProvider
    {
        public IEnumerable<Feature> GetFeatures()
        {
            var features = new List<Feature>();

            try
            {
                var featuresSection = PowNetConfiguration.PowNetSection["Features"];
                if (featuresSection != null)
                {
                    foreach (var featureNode in featuresSection.AsObject())
                    {
                        var feature = new Feature(featureNode.Key);
                        
                        if (featureNode.Value?["Description"] != null)
                            feature.Description = featureNode.Value["Description"]!.ToString();
                        
                        if (featureNode.Value?["Category"] != null)
                            feature.Category = featureNode.Value["Category"]!.ToString();
                        
                        if (featureNode.Value?["DefaultEnabled"] != null)
                            feature.DefaultEnabled = bool.Parse(featureNode.Value["DefaultEnabled"]!.ToString());

                        features.Add(feature);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException("Failed to load features from configuration")
                    .AddParam("Error", ex.Message);
            }

            return features;
        }
    }

    /// <summary>
    /// File-based feature provider
    /// </summary>
    public class FileFeatureProvider : IAsyncFeatureProvider
    {
        public string FilePath { get; set; }

        public FileFeatureProvider(string filePath)
        {
            FilePath = filePath;
        }

        public IEnumerable<Feature> GetFeatures()
        {
            return GetFeaturesAsync().GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<Feature>> GetFeaturesAsync()
        {
            if (!File.Exists(FilePath))
                return Enumerable.Empty<Feature>();

            try
            {
                var json = await File.ReadAllTextAsync(FilePath);
                // Parse JSON and create features
                // This is a simplified implementation
                return new List<Feature>();
            }
            catch (Exception ex)
            {
                throw new PowNetConfigurationException($"Failed to load features from file: {FilePath}")
                    .AddParam("FilePath", FilePath)
                    .AddParam("Error", ex.Message);
            }
        }
    }

    #endregion
}