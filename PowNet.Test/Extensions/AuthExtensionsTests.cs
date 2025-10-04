using System.Security.Claims;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;
using PowNet.Configuration;

namespace PowNet.Test.Extensions
{
    public class AuthExtensionsTests
    {
        [Fact]
        public void Password_Hash_And_Verify_Should_Work()
        {
            var hp = "secret".HashPassword();
            "secret".VerifyPassword(hp).Should().BeTrue();
            "wrong".VerifyPassword(hp).Should().BeFalse();
        }

        [Fact]
        public void ApiKey_Generate_Hash_Verify_Should_Work()
        {
            var key = AuthExtensions.GenerateApiKey(16);
            key.Length.Should().Be(16);
            var hash = key.HashApiKey();
            key.VerifyApiKey(hash).Should().BeTrue();
        }

        [Fact]
        public void Jwt_Generate_And_Validate_Should_Work()
        {
            PowNetConfiguration.SetConfigValue("PowNet:EncryptionSecret", "0123456789ABCDEF0123456789ABCDEF");
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "user")
            }, "test");
            var principal = new ClaimsPrincipal(identity);
            var token = principal.GenerateJwtToken(TimeSpan.FromMinutes(5), audience: "PowNet-Client");
            token.Should().NotBeNullOrEmpty();
            var parsed = token.ValidateJwtToken(audience: "PowNet-Client");
            parsed.Should().NotBeNull();
            parsed!.GetUserName().Should().Be("user");
        }

        [Fact]
        public void Claims_Helpers_And_CSP_Should_Work()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, "admin"),
                new Claim(ClaimTypes.Email, "a@b.com")
            }, "test");
            var principal = new ClaimsPrincipal(identity);
            principal.HasRole("admin").Should().BeTrue();
            principal.HasAnyRole("user","admin").Should().BeTrue();
            principal.HasAllRoles("admin").Should().BeTrue();
            principal.GetEmail().Should().Be("a@b.com");

            var csp = AuthExtensions.GenerateCSPHeader();
            csp.Should().Contain("default-src");
        }

        [Fact]
        public void RateLimiter_Should_Track()
        {
            var id = "id-1";
            id.RecordRequest();
            id.IsWithinRateLimit(100, TimeSpan.FromSeconds(1)).Should().BeTrue();
            id.GetRemainingRequests(100, TimeSpan.FromSeconds(1)).Should().BeLessThanOrEqualTo(100);
        }
    }
}
