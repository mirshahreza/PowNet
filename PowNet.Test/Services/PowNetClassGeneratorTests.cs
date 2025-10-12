using FluentAssertions;
using Xunit;
using PowNet.Services;
using PowNet.Common; // for MethodTemplate enum

namespace PowNet.Test.Services
{
    public class PowNetClassGeneratorTests
    {
        [Fact]
        public void ClassGenerator_ToCode_Should_Produce_Code_With_Custom_Usings()
        {
            var gen = new PowNetClassGenerator("MyClass", "Ns")
                .AddUsing("using System.Text.Json")
                .AddUsing("System.Linq") // normalization test
                .AddUsing("System.Linq"); // duplicate should be ignored

            gen.JqlModelMethods.Add("RunDialog");
            gen.NotMappedMethods.Add("Ping");
            gen.DbProducerMethods["Proc1"] = new List<string>{"int a", "string b"};

            var code = gen.ToCode();
            code.Should().Contain("using System;");
            code.Should().Contain("using System.Text.Json;");
            code.Should().Contain("using System.Linq;");
            code.Split('\n').Count(l => l.Contains("using System.Linq;")).Should().Be(1);
            code.Should().Contain("namespace Ns");
            code.Should().Contain("static class MyClass");
            code.Should().Contain("RunDialog");
            code.Should().Contain("Proc1");
        }

        [Theory]
        [InlineData(MethodTemplate.JqlMethod)]
        [InlineData(MethodTemplate.DbProducer)]
        [InlineData(MethodTemplate.DbScalarFunction)]
        [InlineData(MethodTemplate.DbTableFunction)]
        [InlineData(MethodTemplate.NotMapped)]
        public void MethodGenerator_Should_Return_Template(MethodTemplate mt)
        {
            var mg = new PowNetMethodGenerator("M", mt, new List<string>{"int x"});
            var impl = mg.MethodImplementation;
            impl.Should().NotBeNull();
            impl.Should().Contain("M");
        }
    }
}
