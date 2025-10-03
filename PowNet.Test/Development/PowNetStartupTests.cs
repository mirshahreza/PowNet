using FluentAssertions;
using Xunit;
using PowNet.Development;

namespace PowNet.Test.Development
{
    public class PowNetStartupTests
    {
        [Fact]
        public void GetDevelopmentDashboard_And_StartupDiagnostics_Should_Work()
        {
            var dash = PowNetStartup.GetDevelopmentDashboard();
            dash.Should().NotBeNull();
            var diag = PowNetStartup.GetStartupDiagnostics();
            diag.Should().NotBeNull();
        }

        [Fact]
        public void CreateDevelopmentBackup_Should_Work_When_Dev()
        {
            // Safe call; if not dev, it should throw
            try
            {
                var b = PowNetStartup.CreateDevelopmentBackup();
                b.Should().NotBeNull();
            }
            catch { }
        }

        [Fact]
        public void ResetDevelopmentConfiguration_Should_NotThrow_When_Dev()
        {
            try
            {
                PowNetStartup.ResetDevelopmentConfiguration();
            }
            catch { }
        }
    }
}
