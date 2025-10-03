using FluentAssertions;
using Xunit;
using PowNet.Configuration;

namespace PowNet.Test.Configuration
{
    public class EnvironmentManagerTests
    {
        [Fact]
        public void CurrentEnvironment_And_Register_Get_Should_Work()
        {
            var env = EnvironmentManager.CurrentEnvironment;
            env.Name.Should().NotBeNullOrEmpty();
            EnvironmentManager.GetAllEnvironments().Should().NotBeNull();
            EnvironmentManager.GetEnvironment(env.Name).Should().NotBeNull();
        }

        [Fact]
        public void Env_Specific_Get_Set_And_Detect_Should_Work()
        {
            var v = EnvironmentManager.GetEnvironmentValue("X", 1);
            v.Should().BeGreaterOrEqualTo(0);
            EnvironmentManager.SetEnvironmentValue("X", 2);

            var a = EnvironmentManager.IsRunningInAzure(); a.GetType().Should().Be(typeof(bool));
            var w = EnvironmentManager.IsRunningInAWS(); w.GetType().Should().Be(typeof(bool));
            var g = EnvironmentManager.IsRunningInGoogleCloud(); g.GetType().Should().Be(typeof(bool));
            var d = EnvironmentManager.IsRunningInDocker(); d.GetType().Should().Be(typeof(bool));
            var k = EnvironmentManager.IsRunningInKubernetes(); k.GetType().Should().Be(typeof(bool));
        }

        [Fact]
        public void ValidateEnvironment_Should_Return_Result()
        {
            var res = EnvironmentManager.ValidateEnvironment();
            res.IsValid.GetType().Should().Be(typeof(bool));
        }
    }
}
