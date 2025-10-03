using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Configuration
{
    public class AdditionalConfigurationExtensionsTests
    {
        [Fact]
        public void GetRequired_Should_Throw_When_Missing()
        {
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["A:Count"] = "5"
            }).Build();

            cfg.GetRequired<int>("A:Count").Should().Be(5);
            Action act = () => cfg.GetRequired<string>("A:Name");
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void BindOrDefault_Should_Bind_Into_Instance()
        {
            var dict = new Dictionary<string,string?>
            {
                ["App:Name"] = "PowNet",
                ["App:Enabled"] = "true"
            };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

            var target = new AppOptions { Name = "X", Enabled = false };
            var bound = cfg.BindOrDefault("App", target);
            bound.Name.Should().Be("PowNet");
            bound.Enabled.Should().BeTrue();
            ReferenceEquals(bound, target).Should().BeTrue();
        }

        private class AppOptions { public string Name { get; set; } = ""; public bool Enabled { get; set; } }
    }
}
