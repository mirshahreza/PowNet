using FluentAssertions;
using Xunit;
using PowNet.Features;

namespace PowNet.Test.Features
{
    public class FeatureManagerTests
    {
        [Fact]
        public void Register_IsEnabled_Config_GetFeatureConfig_Should_Work()
        {
            FeatureManager.InitializeBuiltInFeatures();
            FeatureManager.RegisterFeature(new Feature("X") { DefaultEnabled = true });
            FeatureManager.IsEnabled("X").Should().BeTrue();
            FeatureManager.EnableFeature("X");
            FeatureManager.IsEnabled("X").Should().BeTrue();
            FeatureManager.DisableFeature("X");
            FeatureManager.IsEnabled("X").Should().BeFalse();

            var cfg = FeatureManager.GetFeatureConfig<int>("CacheEnabled", "DefaultExpirationMinutes", -1);
            cfg.Should().BeGreaterOrEqualTo(-1);
        }

        [Fact]
        public void Providers_Should_Load()
        {
            var p = new ConfigurationFeatureProvider();
            var list = p.GetFeatures();
            list.Should().NotBeNull();
            var fp = new FileFeatureProvider("features.json");
            var list2 = fp.GetFeatures();
            list2.Should().NotBeNull();
        }
    }
}
