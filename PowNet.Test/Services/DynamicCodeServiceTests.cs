using FluentAssertions;
using Xunit;
using PowNet.Services;
using System.Reflection;

namespace PowNet.Test.Services
{
    public class DynamicCodeServiceTests
    {
        [Fact]
        public void MethodPartsNames_Should_Parse()
        {
            var t = DynamicCodeService.MethodPartsNames("Ns.Class.Method");
            t.Item1.Should().Be("Ns");
            t.Item2.Should().Be("Class");
            t.Item3.Should().Be("Method");
        }

        [Fact]
        public void ArgsToSqlArgs_And_NeedSingleQuote_Should_Work()
        {
            var list = new List<string> { "int a", "string b" };
            var asm = typeof(PowNetClassGenerator).Assembly;
            var t = asm.GetType("PowNet.Services.CSharpTemplates", throwOnError: true)!;
            var method = t.GetMethod("ArgsToSqlArgs", BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            var s = (string)method!.Invoke(null, new object?[]{ list })!;
            s.Should().Contain("{a}");
            s.Should().Contain("'{b}'");
        }
    }
}
