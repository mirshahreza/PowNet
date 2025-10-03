using PowNet.Configuration;
using PowNet.Features;
using PowNet.Logging;
using PowNet.Monitoring;

namespace PowNet.Development
{
    /// <summary>
    /// Development startup configuration and initialization for PowNet framework
    /// </summary>
    public static class PowNetStartup
    {
        /// <summary>
        /// Initialize PowNet framework for development
        /// </summary>
        public static void InitializeDevelopment()
        {
            // Initialize logging first
            PowNetLogger.Initialize();
            var logger = PowNetLogger.GetLogger("Startup");
            
            logger.LogInformation("Initializing PowNet framework in {Environment} environment", 
                PowNetConfiguration.Environment);

            try
            {
                // Initialize feature management
                InitializeFeatureManagement();
                
                // Initialize configuration monitoring
                InitializeConfigurationMonitoring();
                
                // Initialize development-specific features
                if (PowNetConfiguration.IsDevelopment)
                {
                    InitializeDevelopmentFeatures();
                }

                // Validate configuration
                ValidateConfiguration();

                logger.LogInformation("PowNet framework initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogException(ex, "Failed to initialize PowNet framework");
                throw;
            }
        }

        /// <summary>
        /// Initialize PowNet framework for production
        /// </summary>
        public static void InitializeProduction()
        {
            // Initialize logging for production
            PowNetLogger.Initialize();
            var logger = PowNetLogger.GetLogger("Startup");
            
            logger.LogInformation("Initializing PowNet framework for production");

            try
            {
                // Initialize core features only
                FeatureManager.InitializeBuiltInFeatures();
                
                // Load features from configuration
                FeatureManager.RegisterProvider(new Features.ConfigurationFeatureProvider());
                FeatureManager.LoadFeaturesFromProviders();

                // Start configuration monitoring with longer intervals for production
                ConfigurationMonitor.SetHealthCheckInterval(TimeSpan.FromMinutes(15));

                // Validate critical configuration
                ValidateProductionConfiguration();

                logger.LogInformation("PowNet framework initialized for production");
            }
            catch (Exception ex)
            {
                logger.LogCritical("Failed to initialize PowNet framework for production: {Error}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Get development dashboard information
        /// </summary>
        public static object GetDevelopmentDashboard()
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                return new { error = "Development dashboard only available in development environment" };
            }

            var healthReport = ConfigurationMonitor.GetHealthReport();
            var appHealth = Diagnostics.DiagnosticsManager.GetHealthReport();
            var features = FeatureManager.GetAllFeatures();

            return new
            {
                timestamp = DateTime.UtcNow,
                environment = new
                {
                    name = PowNetConfiguration.Environment,
                    isDevelopment = PowNetConfiguration.IsDevelopment,
                    isProduction = PowNetConfiguration.IsProduction,
                    machineName = Environment.MachineName
                },
                health = new
                {
                    overall = healthReport.OverallStatus.ToString(),
                    checks = healthReport.Checks.Select(c => new
                    {
                        name = c.Name,
                        status = c.Status.ToString(),
                        message = c.ErrorMessage ?? "OK"
                    })
                },
                performance = new
                {
                    memoryUsageMB = appHealth.MemoryInfo.TotalMemory / 1024 / 1024,
                    workingSetMB = appHealth.MemoryInfo.WorkingSet / 1024 / 1024,
                    gcCollections = new
                    {
                        gen0 = appHealth.MemoryInfo.Gen0Collections,
                        gen1 = appHealth.MemoryInfo.Gen1Collections,
                        gen2 = appHealth.MemoryInfo.Gen2Collections
                    },
                    threadCount = appHealth.ProcessInfo.ThreadCount,
                    handleCount = appHealth.ProcessInfo.HandleCount
                },
                features = features.Select(f => new
                {
                    name = f.Name,
                    description = f.Description,
                    category = f.Category,
                    enabled = f.IsEnabled(Features.FeatureContext.Default),
                    defaultEnabled = f.DefaultEnabled
                }),
                configuration = new
                {
                    logLevel = PowNetConfiguration.LogLevel,
                    fileLogging = PowNetConfiguration.EnableFileLogging,
                    cacheExpiration = PowNetConfiguration.DefaultCacheExpirationMinutes,
                    maxConcurrentRequests = PowNetConfiguration.MaxConcurrentRequests,
                    requireHttps = PowNetConfiguration.RequireHttps
                }
            };
        }

        #region Private Initialization Methods

        private static void InitializeFeatureManagement()
        {
            // Initialize built-in features
            FeatureManager.InitializeBuiltInFeatures();
            
            // Register configuration provider
            FeatureManager.RegisterProvider(new Features.ConfigurationFeatureProvider());
            
            // Register file provider if exists
            var featuresFile = "features.json";
            if (File.Exists(featuresFile))
            {
                FeatureManager.RegisterProvider(new Features.FileFeatureProvider(featuresFile));
            }
            
            // Load features from all providers
            FeatureManager.LoadFeaturesFromProviders();
        }

        private static void InitializeConfigurationMonitoring()
        {
            // Start configuration health monitoring
            ConfigurationMonitor.SetHealthCheckInterval(
                PowNetConfiguration.IsDevelopment 
                    ? TimeSpan.FromMinutes(1) 
                    : TimeSpan.FromMinutes(5)
            );

            // Monitor critical configuration keys
            ConfigurationMonitor.MonitorConfigurationKey<string>("PowNet:EncryptionSecret", 
                (oldVal, newVal) =>
                {
                    var logger = PowNetLogger.GetLogger("ConfigMonitor");
                    logger.LogWarning("Encryption secret changed at runtime - application restart recommended");
                });

            ConfigurationMonitor.MonitorConfigurationKey<string>("Logging:LogLevel:Default",
                (oldVal, newVal) =>
                {
                    var logger = PowNetLogger.GetLogger("ConfigMonitor");
                    logger.LogInformation("Log level changed from {OldLevel} to {NewLevel}", oldVal, newVal);
                    PowNetLogger.SetGlobalLogLevel(Enum.Parse<Logging.LogLevel>(newVal));
                });
        }

        private static void InitializeDevelopmentFeatures()
        {
            var logger = PowNetLogger.GetLogger("DevStartup");
            
            // Enable development-specific features
            FeatureManager.EnableFeature("DebugMode");
            FeatureManager.EnableFeature("DetailedLogging");
            FeatureManager.EnableFeature("HotReload");
            
            // Register development tools
            logger.LogDebug("Development tools initialized");
            
            // Setup automatic performance monitoring
            if (FeatureManager.IsEnabled("PerformanceMonitoring"))
            {
                logger.LogDebug("Performance monitoring enabled for development");
            }
        }

        private static void ValidateConfiguration()
        {
            var logger = PowNetLogger.GetLogger("ConfigValidation");
            
            try
            {
                var configValidation = PowNetConfiguration.ValidateConfiguration();
                configValidation.ThrowIfInvalid();
                
                var envValidation = EnvironmentManager.ValidateEnvironment();
                if (!envValidation.IsValid)
                {
                    logger.LogWarning("Environment validation issues found: {IssueCount} errors, {WarningCount} warnings",
                        envValidation.Errors.Count(), envValidation.Warnings.Count());
                    
                    foreach (var error in envValidation.Errors.Take(5))
                    {
                        logger.LogError("Environment error: {Message}", error.Message);
                    }
                }
                
                logger.LogInformation("Configuration validation completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogException(ex, "Configuration validation failed");
                throw;
            }
        }

        private static void ValidateProductionConfiguration()
        {
            var logger = PowNetLogger.GetLogger("ProductionValidation");
            
            // Strict validation for production
            var configValidation = PowNetConfiguration.ValidateConfiguration();
            if (!configValidation.IsValid)
            {
                logger.LogCritical("Production configuration validation failed with {ErrorCount} errors",
                    configValidation.Errors.Count);
                
                foreach (var error in configValidation.Errors)
                {
                    logger.LogError("Configuration error: {Error}", error);
                }
                
                throw new InvalidOperationException("Production configuration validation failed");
            }

            // Validate security settings
            if (PowNetConfiguration.EncryptionSecret == "PowNet-Default-Secret-Key")
            {
                throw new InvalidOperationException("Default encryption secret cannot be used in production");
            }

            if (!PowNetConfiguration.RequireHttps)
            {
                logger.LogWarning("HTTPS is not required in production - security risk");
            }

            logger.LogInformation("Production configuration validation passed");
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Create development configuration backup
        /// </summary>
        public static string CreateDevelopmentBackup()
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                throw new InvalidOperationException("Development backup only available in development environment");
            }

            return PowNetConfiguration.CreateConfigurationBackup();
        }

        /// <summary>
        /// Reset to default development configuration
        /// </summary>
        public static void ResetDevelopmentConfiguration()
        {
            if (!PowNetConfiguration.IsDevelopment)
            {
                throw new InvalidOperationException("Configuration reset only available in development environment");
            }

            var logger = PowNetLogger.GetLogger("DevReset");
            
            try
            {
                // Reset to development defaults
                PowNetConfiguration.SetConfigValue("Logging:LogLevel:Default", "Debug");
                PowNetConfiguration.SetConfigValue("PowNet:Security:RequireHttps", false);
                PowNetConfiguration.SetConfigValue("PowNet:Performance:EnableCaching", false);
                PowNetConfiguration.SetConfigValue("PowNet:Logging:EnableFileLogging", true);

                // Refresh configuration
                PowNetConfiguration.RefreshSettings();
                
                // Reinitialize logging with new settings
                PowNetLogger.Initialize();
                
                logger.LogInformation("Development configuration reset to defaults");
            }
            catch (Exception ex)
            {
                logger.LogException(ex, "Failed to reset development configuration");
                throw;
            }
        }

        /// <summary>
        /// Get startup diagnostics information
        /// </summary>
        public static object GetStartupDiagnostics()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            
            return new
            {
                timestamp = DateTime.UtcNow,
                framework = new
                {
                    version = Environment.Version.ToString(),
                    is64Bit = Environment.Is64BitProcess,
                    clrVersion = Environment.Version
                },
                process = new
                {
                    id = process.Id,
                    name = process.ProcessName,
                    startTime = process.StartTime,
                    workingSet = process.WorkingSet64,
                    privateMemory = process.PrivateMemorySize64
                },
                environment = new
                {
                    PowNetEnvironment = PowNetConfiguration.Environment,
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    currentDirectory = Environment.CurrentDirectory
                },
                configuration = new
                {
                    configFiles = GetConfigurationFiles(),
                    workspacePath = PowNetConfiguration.WorkspacePath,
                    logsPath = PowNetConfiguration.LogsPath
                }
            };
        }

        private static List<string> GetConfigurationFiles()
        {
            var files = new List<string>();
            
            if (File.Exists("appsettings.json"))
                files.Add("appsettings.json");
                
            var envFile = $"appsettings.{PowNetConfiguration.Environment}.json";
            if (File.Exists(envFile))
                files.Add(envFile);
                
            if (File.Exists("features.json"))
                files.Add("features.json");
                
            return files;
        }

        #endregion
    }
}