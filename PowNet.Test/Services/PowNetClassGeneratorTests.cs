using FluentAssertions;
using Xunit;
using PowNet.Services;
using PowNet.Common;

namespace PowNet.Test.Services
{
    public class PowNetClassGeneratorTests
    {
        [Fact]
        public void ClassGenerator_ToCode_Should_Produce_Code()
        {
            var gen = new PowNetClassGenerator("MyClass", "Ns");
            gen.DbDialogMethods.Add("RunDialog");
            gen.NotMappedMethods.Add("Ping");
            gen.DbProducerMethods["Proc1"] = new List<string>{"int a", "string b"};
            var code = gen.ToCode();
            code.Should().Contain("namespace Ns");
            code.Should().Contain("static class MyClass");
            code.Should().Contain("RunDialog");
        }

        [Theory]
        [InlineData(MethodTemplate.DbDialog)]
        [InlineData(MethodTemplate.DbProducer)]
        [InlineData(MethodTemplate.DbScalarFunction)]
        [InlineData(MethodTemplate.DbTableFunction)]
        [InlineData(MethodTemplate.NotMapped)]
        public void MethodGenerator_Should_Return_Template(MethodTemplate mt)
        {
            var mg = new PowNetMethodGenerator("M", mt, new List<string>{"int x"});
            var impl = mg.MethodImplementation;
            impl.Should().NotBeNull();
        }
    }
}
