using FluentAssertions;
using Xunit;
using PowNet.Extensions;
using PowNet.Configuration;

namespace PowNet.Test.Extensions
{
    public class ConfigurationExtensionsTests
    {
        private class SampleConf { public int A {get;set;} public string? B {get;set;} }

        [Fact]
        public void Bind_And_Validate_And_Lists_Should_Work()
        {
            var cfg = "Sample".BindConfiguration(new SampleConf{ A = 1, B = "x"});
            cfg.A.Should().Be(1);

            var valid = "Sample".BindAndValidateConfiguration<SampleConf>(c => PowNet.Extensions.SecurityExtensions.ValidateEmail("a@b.com"));
            valid.Should().NotBeNull();

            var lst = "ListKey".GetConfigList<string>(',');
            lst.Should().NotBeNull();

            var dict = "DictKey".GetConfigDictionary<string>();
            dict.Should().NotBeNull();
        }

        [Fact]
        public void Templates_Docs_Compare_Migrate_Backup_Should_Work()
        {
            var tpl = ConfigurationExtensions.GenerateConfigurationTemplate<SampleConf>("S", includeComments:false);
            tpl.Should().Contain("S");
            var doc = ConfigurationExtensions.GenerateConfigurationDocumentation<SampleConf>("S");
            doc.Should().Contain("Configuration Documentation");

            var d1 = new SampleConf{ A = 1, B = "x"};
            var d2 = new SampleConf{ A = 2, B = "x"};
            var diff = ConfigurationExtensions.CompareConfigurations(d1, d2);
            diff.Changes.Should().NotBeEmpty();
            var rpt = ConfigurationExtensions.GenerateChangeReport(diff);
            rpt.Should().Contain("Changes");

            var migrated = ConfigurationExtensions.MigrateConfiguration(d1, o => new SampleConf{ A = o.A, B = o.B });
            migrated.A.Should().Be(1);

            var backup = ConfigurationExtensions.CreateDetailedBackup();
            backup.Environment.Should().NotBeNullOrEmpty();
        }
    }
}
