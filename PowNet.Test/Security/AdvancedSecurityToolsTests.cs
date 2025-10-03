using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Security
{
    public class AdvancedSecurityToolsTests
    {
        [Fact]
        public async Task GenerateSecurePassword_Entropy_And_Compromised_Check()
        {
            var pwd = AdvancedSecurityTools.GenerateSecurePassword(16);
            pwd.Length.Should().Be(16);
            AdvancedSecurityTools.CalculatePasswordEntropy(pwd).Should().BeGreaterThan(0);
            (await AdvancedSecurityTools.IsPasswordCompromisedAsync("password")).Should().BeTrue();
        }

        [Fact]
        public void SanitizeHtml_FileName_And_Csv()
        {
            var html = "<script>x</script><a href=\"javascript:alert(1)\">x</a>".SanitizeHtmlContent();
            html.Should().NotContain("script");
            html.Should().NotContain("javascript:");

            var name = "bad?.txt".SanitizeFileName();
            name.Should().NotContain("?");

            var csv = "=cmd|'\"/c calc'".SanitizeCsvField();
            csv.StartsWith("=").Should().BeFalse();
        }

        [Fact]
        public void Tokens_TOTP_Signatures_And_Headers()
        {
            var t = AdvancedSecurityTools.GenerateSecureToken(16, TokenFormat.Alphanumeric);
            t.Should().NotBeNullOrEmpty();
            var secret = "s";
            var code = AdvancedSecurityTools.GenerateTOTP(secret);
            AdvancedSecurityTools.VerifyTOTP(secret, code).Should().BeTrue();

            var sig = AdvancedSecurityTools.CreateDigitalSignature("data", "k", SignatureAlgorithm.HMAC_SHA256);
            AdvancedSecurityTools.VerifyDigitalSignature("data", sig, "k", SignatureAlgorithm.HMAC_SHA256).Should().BeTrue();

            var headers = AdvancedSecurityTools.GenerateSecurityHeaders();
            headers.Should().ContainKey("Strict-Transport-Security");
        }
    }
}
