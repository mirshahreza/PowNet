using System.Reflection;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class SystemExtensionsTests
    {
        private class Dummy
        {
            public int Foo() => 42;
            public override string ToString() => base.ToString();
            public override bool Equals(object? obj) => base.Equals(obj);
            public override int GetHashCode() => base.GetHashCode();
        }

        [Fact]
        public void GetFullName_Should_Return_Type_And_Method_Name()
        {
            var mi = typeof(Dummy).GetMethod(nameof(Dummy.Foo))!;
            var fn = mi.GetFullName();
            fn.Should().Contain(nameof(Dummy));
            fn.Should().EndWith("." + nameof(Dummy.Foo));
        }

        [Fact]
        public void GetMethodsReal_Should_Filter_Object_Methods()
        {
            var methods = typeof(Dummy).GetMethodsReal();
            methods.Should().OnlyContain(m => m.Name != nameof(object.ToString)
                                           && m.Name != nameof(object.Equals)
                                           && m.Name != nameof(object.GetHashCode)
                                           && m.Name != nameof(object.GetType));
            methods.Should().ContainSingle(m => m.Name == nameof(Dummy.Foo));
        }

        [Fact]
        public void IsRealType_Should_Return_True_For_Any_String()
        {
            SystemExtensions.IsRealType("anything").Should().BeTrue();
        }

        [Fact]
        public void GetTypesReal_Should_Filter_Internal_Attributes()
        {
            var asm = typeof(SystemExtensionsTests).Assembly;
            var types = asm.GetTypesReal();
            types.Should().NotContain(t => t.Name.Contains("EmbeddedAttribute") || t.Name.Contains("RefSafetyRulesAttribute"));
        }

        [Fact]
        public void GetPlaceInfo_Should_Format_As_Type_And_Method()
        {
            MethodBase? mb = typeof(Dummy).GetMethod(nameof(Dummy.Foo));
            var info = mb.GetPlaceInfo();
            info.Should().Contain(nameof(Dummy));
            info.Should().Contain(nameof(Dummy.Foo));
            ((MethodBase?)null).GetPlaceInfo().Should().Be("");
        }
    }
}
