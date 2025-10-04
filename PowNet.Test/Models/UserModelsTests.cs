using FluentAssertions;
using Xunit;
using PowNet.Models;
using PowNet.Configuration;
using PowNet.Extensions;

namespace PowNet.Test.Models
{
    public class UserModelsTests
    {
        [Fact]
        public void UserServerObject_Cache_And_Token_Should_Work()
        {
            var u = new UserServerObject { Id = 1, UserName = "u" };
            u.ToCache();
            var cached = UserServerObject.FromCache("u");
            cached.Should().NotBeNull();

            var token = u.Tokenize();
            token.Should().NotBeNullOrEmpty();
            var json = token.DecryptAesGcm(PowNetConfiguration.EncryptionSecret);
            json.Should().Contain("u");
        }

        [Fact]
        public void ToClient_And_TokenObject_Should_Work()
        {
            var u = new UserServerObject { Id = 1, UserName = "u" };
            var client = u.ToClientVersion();
            client.UserName.Should().Be("u");
            var to = u.ToTokenVersion();
            to.UserName.Should().Be("u");
        }

        [Fact]
        public void Access_Rules_Should_Work()
        {
            var u = new UserServerObject { Id = 1, UserName = "u", Roles = new(){ new Role{ Id=1, RoleName="r" } } };
            var api = new PowNet.Configuration.ApiConfiguration{ CacheLevel = PowNet.Common.CacheLevel.AllUsers, CacheSeconds = 1, AllowedRoles = new(){1}};
            u.HasAccess(api).Should().BeTrue();
        }
    }
}
