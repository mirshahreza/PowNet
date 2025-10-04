using FluentAssertions;
using PowNet.Configuration;
using PowNet.Models;
using PowNet.Common;
using Xunit;

namespace PowNet.Test.Models
{
    public class AccessRulesTests
    {
        [Fact]
        public void HasAccess_Should_Deny_When_In_DeniedUsers()
        {
            var user = new UserServerObject{ Id = 5, UserName = "u" };
            var cfg = new ApiConfiguration{ DeniedUsers = new(){5} };
            user.HasAccess(cfg).Should().BeFalse();
        }

        [Fact]
        public void HasAccess_Should_Deny_When_DeniedRole_Intersect()
        {
            var user = new UserServerObject{ Id = 5, UserName = "u", Roles = new(){ new Role{ Id=2, RoleName="R2" } } };
            var cfg = new ApiConfiguration{ DeniedRoles = new(){2} };
            user.HasAccess(cfg).Should().BeFalse();
        }

        [Fact]
        public void HasAccess_Should_Allow_When_AllowedRoles_Intersect()
        {
            var user = new UserServerObject{ Id = 5, UserName = "u", Roles = new(){ new Role{ Id=3, RoleName="R3" } } };
            var cfg = new ApiConfiguration{ AllowedRoles = new(){3} };
            user.HasAccess(cfg).Should().BeTrue();
        }

        [Fact]
        public void HasAccess_Should_Allow_When_AllowedUsers_Contains_User()
        {
            var user = new UserServerObject{ Id = 7, UserName = "u" };
            var cfg = new ApiConfiguration{ AllowedUsers = new(){7} };
            user.HasAccess(cfg).Should().BeTrue();
        }
    }
}
