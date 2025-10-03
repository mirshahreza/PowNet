using System.Text;
using System.Text.Json;
using PowNet.Services;

namespace PowNet.Configuration
{
    public record ControllerConfiguration
    {
        public required string NamespaceName { get; set; }
        public required string ControllerName { get; set; }
        public List<ApiConfiguration> ApiConfigurations { get; set; } = [];

        public static ControllerConfiguration GetConfig(string controllerFullName)
        {
            string namespaceName = controllerFullName.Split('.').SkipLast(1).FirstOrDefault() ?? "";
            string controllerName = controllerFullName.Split('.').LastOrDefault() ?? "";
            return GetConfig(namespaceName, controllerName);
        }

        public static ControllerConfiguration GetConfig(string namespaceName, string controllerName)
        {
            string key = GenerateCacheKey(namespaceName, controllerName);
            MemoryService.SharedMemoryCache.TryGetValue(key, out var config);
            if (config == null)
            {
                config = ReadConfig(namespaceName, controllerName);
                MemoryService.SharedMemoryCache.TryAdd(key, config);
            }
            return (ControllerConfiguration)config;
        }

        public static ControllerConfiguration ReadConfig(string controllerFullName)
        {
            string namespaceName = controllerFullName.Split('.').SkipLast(1).FirstOrDefault() ?? "";
            string controllerName = controllerFullName.Split('.').LastOrDefault() ?? "";
            return ReadConfig(namespaceName, controllerName);
        }

        public static ControllerConfiguration ReadConfig(string namespaceName, string controllerName)
        {
            string cFileName = GenerateConfigFileName(namespaceName, controllerName);
            if (File.Exists(cFileName))
            {
                var json = File.ReadAllText(cFileName, Encoding.UTF8);
                return JsonSerializer.Deserialize<ControllerConfiguration>(json) ?? new ControllerConfiguration() { NamespaceName = namespaceName, ControllerName = controllerName };
            }
            return new ControllerConfiguration() { NamespaceName = namespaceName, ControllerName = controllerName };
        }

        public static void ClearConfigCache(string controllerFullName)
        {
            string namespaceName = controllerFullName.Split('.').SkipLast(1).FirstOrDefault() ?? "";
            string controllerName = controllerFullName.Split('.').LastOrDefault() ?? "";
            ClearConfigCache(namespaceName, controllerName);
        }

        public static void ClearConfigCache(string namespaceName, string controllerName)
        {
            MemoryService.SharedMemoryCache.TryRemove(GenerateCacheKey(namespaceName, controllerName));
        }

        public static string GenerateConfigFileName(string? namespaceName, string controllerName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName)) return $"workspace/server/{controllerName}.cs.config.json";
            else return $"workspace/server/{namespaceName}.{controllerName}.cs.config.json";
        }

        public static string GenerateCacheKey(string namespaceName, string controllerName)
        {
            return $"Conf::{namespaceName}.{controllerName}";
        }
    }

    public static class ControllerConfigurationExtensions
    {
        public static void WriteConfig(this ControllerConfiguration config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(config.GetConfigFileName(), json, Encoding.UTF8);
        }

        public static string GetConfigFileName(this ControllerConfiguration config)
        {
            return ControllerConfiguration.GenerateConfigFileName(config.NamespaceName, config.ControllerName);
        }
    }
}