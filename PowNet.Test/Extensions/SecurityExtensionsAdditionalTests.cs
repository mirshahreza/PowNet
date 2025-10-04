using FluentAssertions;
using PowNet.Extensions;
using Xunit;

namespace PowNet.Test.Extensions
{
    public class SecurityExtensionsAdditionalTests
    {
        [Fact]
        public void ValidatePathSafety_Should_Fail_On_Dangerous_Extension()
        {
            var result = SecurityExtensions.ValidatePathSafety("folder/run.exe", "p");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePathSafety_Should_Fail_On_Reserved_Name()
        {
            var result = SecurityExtensions.ValidatePathSafety("CON.txt", "p");
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePasswordStrength_Should_Fail_On_Common_Password()
        {
            var policy = new PasswordPolicy{ MinLength = 8 };
            var res = SecurityExtensions.ValidatePasswordStrength("password", policy);
            res.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ValidatePasswordStrength_Should_Fail_On_Repeated_Patterns()
        {
            var policy = new PasswordPolicy{ MinLength = 6 };
            var res = SecurityExtensions.ValidatePasswordStrength("aaaaaa", policy);
            res.IsValid.Should().BeFalse();
        }
    }
}
