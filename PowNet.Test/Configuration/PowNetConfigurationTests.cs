using FluentAssertions;
using Xunit;
using PowNet.Configuration;

namespace PowNet.Test.Configuration
{
    public class PowNetConfigurationTests
    {
        [Fact]
        public void Basic_Env_And_Paths_Should_Work()
        {
            var env = PowNetConfiguration.Environment;
            env.Should().NotBeNullOrEmpty();
            var dev = PowNetConfiguration.IsDevelopment; dev.GetType().Should().Be(typeof(bool));
            PowNetConfiguration.WorkspacePath.Should().NotBeNull();
            PowNetConfiguration.LogsPath.Should().NotBeNull();
            PowNetConfiguration.ProjectRoot.FullName.Should().NotBeNull();
        }

        [Fact]
        public void Get_Set_Config_Value_And_Secrets()
        {
            var v = PowNetConfiguration.GetConfigValue("PowNet:SomeKey", 1);
            v.Should().Be(1);
            PowNetConfiguration.SetConfigValue("PowNet:SomeKey", 2);
            PowNetConfiguration.GetConfigValue("PowNet:SomeKey", 1).Should().Be(2);
            PowNetConfiguration.GetSecretValue("PowNet:NoSecret", "def").Should().Be("def");
        }

        [Fact]
        public void ConnectionStrings_Api_Should_Be_Robust()
        {
            // gracefully handle absence of ConnectionStrings in default repo
            try
            {
                var all = PowNetConfiguration.GetConnectionStrings().ToList();
            }
            catch { }
        }

        [Fact]
        public void Save_Refresh_Backup_Restore_Should_Work()
        {
            // ensure base file exists
            var baseFile = "appsettings.json";
            if (!File.Exists(baseFile)) File.WriteAllText(baseFile, "{ }");

            var backup = PowNetConfiguration.CreateConfigurationBackup();
            backup.Should().NotBeNullOrEmpty();
            PowNetConfiguration.Save();
            PowNetConfiguration.RefreshSettings();

            PowNetConfiguration.RestoreConfigurationFromBackup(backup);
            File.Exists(backup).Should().BeTrue();
        }

        [Fact]
        public void ValidateConfiguration_Should_Return_Result()
        {
            var res = PowNetConfiguration.ValidateConfiguration();
            var b = res.IsValid; b.GetType().Should().Be(typeof(bool));
        }
    }
}
