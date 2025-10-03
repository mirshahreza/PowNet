using FluentAssertions;
using Xunit;
using PowNet.Common;

namespace PowNet.Test.Common
{
    public class PowNetExceptionTests
    {
        [Fact]
        public void PowNetException_Should_Store_Metadata_And_Report()
        {
            var ex = new PowNetException("Oops!").AddParam("K","V");
            var detailed = ex.GetDetailedReport();
            detailed.Should().Contain("Oops!");
            ex.GetParam<string>("K").Should().Be("V");
        }

        [Fact]
        public void PowNetValidationException_Should_Add_Property_Info()
        {
            var ex = new PowNetValidationException("Invalid","Name", "Bob");
            ex.GetParam<string>("Property").Should().Be("Name");
            ex.GetParam<object>("AttemptedValue").Should().Be("Bob");
        }

        [Fact]
        public void PowNetConfigurationException_Should_Add_Key()
        {
            var ex = new PowNetConfigurationException("Cfg","Key");
            ex.GetParam<string>("ConfigurationKey").Should().Be("Key");
        }

        [Fact]
        public void PowNetSecurityException_Should_Add_Context()
        {
            var ex = new PowNetSecurityException("Sec","CTX");
            ex.GetParam<string>("SecurityContext").Should().Be("CTX");
        }

        [Fact]
        public void GetEx_Should_Copy_Metadata_To_Standard_Exception()
        {
            var ex = new PowNetException("X").AddParam("A", 1).AddParam("B", "bb");
            var std = ex.GetEx();
            std.Data["A"].Should().Be(1);
            std.Data["B"].Should().Be("bb");
        }
    }
}
