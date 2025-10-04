using FluentAssertions;
using PowNet.Configuration;
using PowNet.Models;
using Xunit;

namespace PowNet.Test.Models
{
    public class AccessRulesConflictTests
    {
        [Fact]
        public void DeniedRole_Should_Override_AllowedRole()
        {
            var user = new UserServerObject { Id = 10, UserName = "u", Roles = new(){ new Role{ Id=1, RoleName="R1" } } };
            var cfg = new ApiConfiguration{ AllowedRoles = new(){1}, DeniedRoles = new(){1} };
            user.HasAccess(cfg).Should().BeFalse();
        }
    }
}
