using System.Collections.Concurrent;
using System.Diagnostics;
using PowNet.Configuration;

namespace PowNet.Monitoring
{
    /// <summary>
    /// Configuration monitoring and health checking for PowNet framework
    /// </summary>
    public static class ConfigurationMonitor
    {
        #region Configuration Health Monitoring

        private static readonly ConcurrentDictionary<string, ConfigurationHealth> _healthCache = new();
        private static readonly Timer _healthCheckTimer;
        private static readonly object _lock = new();
        private static TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(5);

        static ConfigurationMonitor()
        {
            _healthCheckTimer = new Timer(PerformHealthChecks, null, _healthCheckInterval, _healthCheckInterval);
        }

        /// <summary>
        /// Get current configuration health status
        /// </summary>
        public static ConfigurationHealthReport GetHealthReport()
        {
            lock (_lock)
            {
                var report = new ConfigurationHealthReport
                {
                    Timestamp = DateTime.UtcNow,
                    OverallStatus = HealthStatus.Healthy
                };

                // Collect all health checks
                foreach (var health in _healthCache.Values)
                {
                    report.Checks.Add(health);
                    
                    if (health.Status == HealthStatus.Unhealthy)
                    {
                        report.OverallStatus = HealthStatus.Unhealthy;
                    }
                    else if (health.Status == HealthStatus.Degraded && report.OverallStatus == HealthStatus.Healthy)
                    {
                        report.OverallStatus = HealthStatus.Degraded;
                    }
                }

                return report;
            }
        }

        /// <summary>
        /// Set health check interval
        /// </summary>
        public static void SetHealthCheckInterval(TimeSpan interval)
        {
            _healthCheckInterval = interval;
            _healthCheckTimer.Change(interval, interval);
        }

        /// <summary>
        /// Perform immediate health check
        /// </summary>
        public static ConfigurationHealthReport PerformImmediateHealthCheck()
        {
            PerformHealthChecks(null);
            return GetHealthReport();
        }

        #endregion

        #region Configuration Change Monitoring

        private static readonly List<ConfigurationChangeHandler> _changeHandlers = new();
        private static readonly ConcurrentDictionary<string, object> _lastKnownValues = new();

        /// <summary>
        /// Register configuration change handler
        /// </summary>
        public static void RegisterChangeHandler(ConfigurationChangeHandler handler)
        {
            lock (_changeHandlers)
            {
                _changeHandlers.Add(handler);
            }
        }

        /// <summary>
        /// Monitor specific configuration key for changes
        /// </summary>
        public static void MonitorConfigurationKey<T>(string key, Action<T, T> onChanged)
        {
            var currentValue = PowNetConfiguration.GetConfigValue<T>(key);
            _lastKnownValues[key] = currentValue!;

            RegisterChangeHandler(new ConfigurationChangeHandler
            {
                Key = key,
                Callback = (oldVal, newVal) =>
                {
                    if (oldVal is T typedOld && newVal is T typedNew)
                    {
                        onChanged(typedOld, typedNew);
                    }
                }
            });
        }

        /// <summary>
        /// Check for configuration changes
        /// </summary>
        public static List<ConfigurationChange> CheckForChanges()
        {
            var changes = new List<ConfigurationChange>();

            foreach (var handler in _changeHandlers.ToList())
            {
                try
                {
                    var currentValue = PowNetConfiguration.GetConfigValue<object>(handler.Key);
                    
                    if (_lastKnownValues.TryGetValue(handler.Key, out var lastValue))
                    {
                        if (!Equals(currentValue, lastValue))
                        {
                            changes.Add(new ConfigurationChange
                            {
                                Key = handler.Key,
                                OldValue = lastValue,
                                NewValue = currentValue,
                                Timestamp = DateTime.UtcNow
                            });

                            handler.Callback?.Invoke(lastValue, currentValue);
                            _lastKnownValues[handler.Key] = currentValue!;
                        }
                    }
                    else
                    {
                        _lastKnownValues[handler.Key] = currentValue!;
                    }
                }
                catch (Exception ex)
                {
                    // Log configuration monitoring error
                    Debug.WriteLine($"Configuration monitoring error for key '{handler.Key}': {ex.Message}");
                }
            }

            return changes;
        }

        #endregion

        #region Performance Monitoring

        private static readonly ConcurrentDictionary<string, ConfigurationPerformanceMetrics> _performanceMetrics = new();

        /// <summary>
        /// Record configuration access performance
        /// </summary>
        public static void RecordConfigurationAccess(string key, TimeSpan accessTime, bool fromCache)
        {
            var metrics = _performanceMetrics.GetOrAdd(key, _ => new ConfigurationPerformanceMetrics { Key = key });
            metrics.RecordAccess(accessTime, fromCache);
        }

        /// <summary>
        /// Get configuration performance metrics
        /// </summary>
        public static IEnumerable<ConfigurationPerformanceMetrics> GetPerformanceMetrics()
        {
            return _performanceMetrics.Values.ToList();
        }

        /// <summary>
        /// Get performance summary
        /// </summary>
        public static ConfigurationPerformanceSummary GetPerformanceSummary()
        {
            var metrics = _performanceMetrics.Values.ToList();
            
            return new ConfigurationPerformanceSummary
            {
                TotalKeys = metrics.Count,
                TotalAccesses = metrics.Sum(m => m.TotalAccesses),
                CacheHitRate = metrics.Count == 0 ? 0 : metrics.Average(m => m.CacheHitRate),
                AverageAccessTime = metrics.Count == 0 ? TimeSpan.Zero : 
                    TimeSpan.FromTicks((long)metrics.Average(m => m.AverageAccessTime.Ticks)),
                SlowestKey = metrics.OrderByDescending(m => m.AverageAccessTime).FirstOrDefault()?.Key ?? "N/A",
                FastestKey = metrics.OrderBy(m => m.AverageAccessTime).FirstOrDefault()?.Key ?? "N/A"
            };
        }

        #endregion

        #region Private Health Check Methods

        private static void PerformHealthChecks(object? state)
        {
            try
            {
                // Configuration file health check
                CheckConfigurationFileHealth();

                // Connection strings health check
                CheckConnectionStringsHealth();

                // Environment validation health check
                CheckEnvironmentHealth();

                // Performance health check
                CheckPerformanceHealth();

                // Security configuration health check
                CheckSecurityConfigurationHealth();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Health check error: {ex.Message}");
            }
        }

        private static void CheckConfigurationFileHealth()
        {
            var health = new ConfigurationHealth
            {
                Name = "ConfigurationFile",
                Description = "Configuration file accessibility and validity"
            };

            try
            {
                var configPath = "appsettings.json";
                var envConfigPath = $"appsettings.{PowNetConfiguration.Environment}.json";

                if (!File.Exists(configPath))
                {
                    health.Status = HealthStatus.Unhealthy;
                    health.ErrorMessage = $"Configuration file not found: {configPath}";
                }
                else
                {
                    var fileInfo = new FileInfo(configPath);
                    health.Data["LastModified"] = fileInfo.LastWriteTime;
                    health.Data["FileSizeBytes"] = fileInfo.Length;

                    if (File.Exists(envConfigPath))
                    {
                        var envFileInfo = new FileInfo(envConfigPath);
                        health.Data["EnvConfigLastModified"] = envFileInfo.LastWriteTime;
                        health.Data["EnvConfigSizeBytes"] = envFileInfo.Length;
                    }

                    // Try to parse configuration
                    try
                    {
                        var _ = PowNetConfiguration.AppSettings;
                        health.Status = HealthStatus.Healthy;
                        health.Description = "Configuration file is accessible and valid";
                    }
                    catch (Exception ex)
                    {
                        health.Status = HealthStatus.Unhealthy;
                        health.ErrorMessage = $"Configuration parsing error: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = $"Configuration file check failed: {ex.Message}";
            }

            _healthCache["ConfigurationFile"] = health;
        }

        private static void CheckConnectionStringsHealth()
        {
            var health = new ConfigurationHealth
            {
                Name = "ConnectionStrings",
                Description = "Connection strings validation"
            };

            try
            {
                var connectionStrings = PowNetConfiguration.GetConnectionStrings().ToList();
                
                if (!connectionStrings.Any())
                {
                    health.Status = HealthStatus.Degraded;
                    health.ErrorMessage = "No connection strings configured";
                }
                else
                {
                    health.Status = HealthStatus.Healthy;
                    health.Data["ConnectionStringCount"] = connectionStrings.Count;
                    
                    var emptyConnections = connectionStrings.Where(cs => string.IsNullOrWhiteSpace(cs.Value)).ToList();
                    if (emptyConnections.Any())
                    {
                        health.Status = HealthStatus.Degraded;
                        health.ErrorMessage = $"Empty connection strings found: {string.Join(", ", emptyConnections.Select(cs => cs.Key))}";
                    }
                }
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = $"Connection strings validation failed: {ex.Message}";
            }

            _healthCache["ConnectionStrings"] = health;
        }

        private static void CheckEnvironmentHealth()
        {
            var health = new ConfigurationHealth
            {
                Name = "Environment",
                Description = "Environment configuration validation"
            };

            try
            {
                var envValidation = EnvironmentManager.ValidateEnvironment();
                
                health.Data["EnvironmentName"] = envValidation.EnvironmentName;
                health.Data["ErrorCount"] = envValidation.Errors.Count();
                health.Data["WarningCount"] = envValidation.Warnings.Count();

                if (envValidation.Errors.Any())
                {
                    health.Status = HealthStatus.Unhealthy;
                    health.ErrorMessage = $"Environment validation errors: {string.Join("; ", envValidation.Errors.Take(3).Select(e => e.Message))}";
                }
                else if (envValidation.Warnings.Any())
                {
                    health.Status = HealthStatus.Degraded;
                    health.ErrorMessage = $"Environment validation warnings: {string.Join("; ", envValidation.Warnings.Take(3).Select(w => w.Message))}";
                }
                else
                {
                    health.Status = HealthStatus.Healthy;
                    health.Description = "Environment configuration is valid";
                }
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = $"Environment validation failed: {ex.Message}";
            }

            _healthCache["Environment"] = health;
        }

        private static void CheckPerformanceHealth()
        {
            var health = new ConfigurationHealth
            {
                Name = "Performance",
                Description = "Configuration access performance"
            };

            try
            {
                var perfSummary = GetPerformanceSummary();
                
                health.Data["TotalKeys"] = perfSummary.TotalKeys;
                health.Data["TotalAccesses"] = perfSummary.TotalAccesses;
                health.Data["CacheHitRate"] = perfSummary.CacheHitRate;
                health.Data["AverageAccessTime"] = perfSummary.AverageAccessTime.TotalMilliseconds;

                if (perfSummary.AverageAccessTime > TimeSpan.FromMilliseconds(100))
                {
                    health.Status = HealthStatus.Degraded;
                    health.ErrorMessage = $"Configuration access is slow: {perfSummary.AverageAccessTime.TotalMilliseconds:F2}ms average";
                }
                else if (perfSummary.CacheHitRate < 0.8 && perfSummary.TotalAccesses > 100)
                {
                    health.Status = HealthStatus.Degraded;
                    health.ErrorMessage = $"Low cache hit rate: {perfSummary.CacheHitRate:P1}";
                }
                else
                {
                    health.Status = HealthStatus.Healthy;
                    health.Description = "Configuration performance is optimal";
                }
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Degraded;
                health.ErrorMessage = $"Performance check failed: {ex.Message}";
            }

            _healthCache["Performance"] = health;
        }

        private static void CheckSecurityConfigurationHealth()
        {
            var health = new ConfigurationHealth
            {
                Name = "SecurityConfiguration",
                Description = "Security-related configuration validation"
            };

            try
            {
                var issues = new List<string>();

                if (PowNetConfiguration.EncryptionSecret == "PowNet-Default-Secret-Key")
                {
                    issues.Add("Using default encryption secret");
                }

                if (PowNetConfiguration.EncryptionSecret.Length < 16)
                {
                    issues.Add("Encryption secret is too short");
                }

                if (PowNetConfiguration.IsProduction && !PowNetConfiguration.RequireHttps)
                {
                    issues.Add("HTTPS not required in production");
                }

                if (PowNetConfiguration.PasswordMinLength < 8)
                {
                    issues.Add("Password minimum length is too low");
                }

                health.Data["PasswordMinLength"] = PowNetConfiguration.PasswordMinLength;
                health.Data["RequireHttps"] = PowNetConfiguration.RequireHttps;
                health.Data["IsProduction"] = PowNetConfiguration.IsProduction;

                if (issues.Any())
                {
                    health.Status = HealthStatus.Degraded;
                    health.ErrorMessage = string.Join("; ", issues);
                }
                else
                {
                    health.Status = HealthStatus.Healthy;
                    health.Description = "Security configuration is properly configured";
                }
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = $"Security configuration check failed: {ex.Message}";
            }

            _healthCache["SecurityConfiguration"] = health;
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Configuration health status
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    /// <summary>
    /// Configuration health information
    /// </summary>
    public class ConfigurationHealth
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public HealthStatus Status { get; set; } = HealthStatus.Healthy;
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Configuration health report
    /// </summary>
    public class ConfigurationHealthReport
    {
        public DateTime Timestamp { get; set; }
        public HealthStatus OverallStatus { get; set; }
        public List<ConfigurationHealth> Checks { get; set; } = new();

        public IEnumerable<ConfigurationHealth> HealthyChecks => Checks.Where(c => c.Status == HealthStatus.Healthy);
        public IEnumerable<ConfigurationHealth> DegradedChecks => Checks.Where(c => c.Status == HealthStatus.Degraded);
        public IEnumerable<ConfigurationHealth> UnhealthyChecks => Checks.Where(c => c.Status == HealthStatus.Unhealthy);
    }

    /// <summary>
    /// Configuration change handler
    /// </summary>
    public class ConfigurationChangeHandler
    {
        public string Key { get; set; } = string.Empty;
        public Action<object?, object?>? Callback { get; set; }
    }

    /// <summary>
    /// Configuration change information
    /// </summary>
    public class ConfigurationChange
    {
        public string Key { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Configuration performance metrics
    /// </summary>
    public class ConfigurationPerformanceMetrics
    {
        private readonly object _lock = new();
        private long _totalAccesses = 0;
        private long _cacheHits = 0;
        private long _totalAccessTimeTicks = 0;

        public string Key { get; set; } = string.Empty;
        public long TotalAccesses => _totalAccesses;
        public long CacheHits => _cacheHits;
        public double CacheHitRate => _totalAccesses == 0 ? 0 : (double)_cacheHits / _totalAccesses;
        public TimeSpan AverageAccessTime => _totalAccesses == 0 ? TimeSpan.Zero : new TimeSpan(_totalAccessTimeTicks / _totalAccesses);

        public void RecordAccess(TimeSpan accessTime, bool fromCache)
        {
            lock (_lock)
            {
                _totalAccesses++;
                _totalAccessTimeTicks += accessTime.Ticks;
                
                if (fromCache)
                {
                    _cacheHits++;
                }
            }
        }
    }

    /// <summary>
    /// Configuration performance summary
    /// </summary>
    public class ConfigurationPerformanceSummary
    {
        public int TotalKeys { get; set; }
        public long TotalAccesses { get; set; }
        public double CacheHitRate { get; set; }
        public TimeSpan AverageAccessTime { get; set; }
        public string SlowestKey { get; set; } = string.Empty;
        public string FastestKey { get; set; } = string.Empty;
    }

    #endregion
}