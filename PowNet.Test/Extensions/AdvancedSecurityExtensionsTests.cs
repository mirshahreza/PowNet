using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class AdvancedSecurityExtensionsTests
    {
        [Fact]
        public async Task GenerateSecurePassword_Entropy_And_Compromised_Check()
        {
            var pwd = AdvancedSecurityExtensions.GenerateSecurePassword(16);
            pwd.Length.Should().Be(16);
            AdvancedSecurityExtensions.CalculatePasswordEntropy(pwd).Should().BeGreaterThan(0);
            (await AdvancedSecurityExtensions.IsPasswordCompromisedAsync("password")).Should().BeTrue();
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
            // Allow either quoting or neutralization; ensure it doesn't start with dangerous prefix
            csv.StartsWith("=").Should().BeFalse();
        }

        [Fact]
        public void Tokens_TOTP_Signatures_And_Headers()
        {
            var t = AdvancedSecurityExtensions.GenerateSecureToken(16, TokenFormat.Alphanumeric);
            t.Should().NotBeNullOrEmpty();
            var secret = "s";
            var code = AdvancedSecurityExtensions.GenerateTOTP(secret);
            AdvancedSecurityExtensions.VerifyTOTP(secret, code).Should().BeTrue();

            var sig = AdvancedSecurityExtensions.CreateDigitalSignature("data", "k", SignatureAlgorithm.HMAC_SHA256);
            AdvancedSecurityExtensions.VerifyDigitalSignature("data", sig, "k", SignatureAlgorithm.HMAC_SHA256).Should().BeTrue();

            var headers = AdvancedSecurityExtensions.GenerateSecurityHeaders();
            headers.Should().ContainKey("Strict-Transport-Security");
        }
    }
}
