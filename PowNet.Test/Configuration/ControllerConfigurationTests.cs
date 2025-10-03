using FluentAssertions;
using Xunit;
using PowNet.Configuration;

namespace PowNet.Test.Configuration
{
    public class ControllerConfigurationTests
    {
        [Fact]
        public void ControllerConfiguration_Cache_Read_Write_Should_Work()
        {
            var namespaceName = "Ns";
            var controllerName = "Ctrl";
            var cfg = ControllerConfiguration.GetConfig(namespaceName, controllerName);
            cfg.NamespaceName.Should().Be(namespaceName);
            cfg.ControllerName.Should().Be(controllerName);

            cfg.ApiConfigurations.Add(new ApiConfiguration{ ApiName = "A1", CacheSeconds = 5});
            // ensure directory exists
            var file = cfg.GetConfigFileName();
            var dir = Path.GetDirectoryName(file)!;
            Directory.CreateDirectory(dir);
            cfg.WriteConfig();

            File.Exists(file).Should().BeTrue();
            var cfg2 = ControllerConfiguration.ReadConfig(namespaceName, controllerName);
            cfg2.ApiConfigurations.Should().NotBeEmpty();

            ControllerConfiguration.ClearConfigCache(namespaceName, controllerName);
            var cfg3 = ControllerConfiguration.GetConfig(namespaceName, controllerName);
            cfg3.Should().NotBeNull();
        }
    }
}
