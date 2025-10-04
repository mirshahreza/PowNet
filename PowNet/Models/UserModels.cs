using System.Text.Json.Nodes;
using PowNet.Configuration;
using PowNet.Extensions;
using PowNet.Abstractions.Authentication; // added

namespace PowNet.Models
{
    public record UserServerObject : IUserIdentity // implement abstraction
    {
        public DateTime GeneratedOn { get; } = DateTime.Now;
        public bool IsPerfect { get; set; } = false;
        public required int Id { get; set; } = 0;
        public required string UserName { get; set; }
        public bool IsPubKey { get; set; } = false;
        public List<Role> Roles { set; get; } = [];
        public List<Action> AllowedActions { set; get; } = [];
        public JsonObject Data { set; get; } = [];

        // IUserIdentity implementation
        public bool IsAnonymous => Id == 0 || string.Equals(UserName, NobodyUserName, StringComparison.OrdinalIgnoreCase);
        public bool IsInRole(string roleName) => Roles?.Any(r => string.Equals(r.RoleName, roleName, StringComparison.OrdinalIgnoreCase)) == true;

        public static string NobodyUserName { get; } = "nobody";

        private static UserServerObject? _userServerObject;
        public static UserServerObject NobodyUserServerObject
        {
            get
            {
                _userServerObject ??= new() { Id = 0, UserName = NobodyUserName, Roles = [], Data = [] };
                return _userServerObject;
            }
        }

        public static UserServerObject? FromCache(string userName)
        {
            Services.MemoryService.SharedMemoryCache.TryGetValue(GetCacheKey(userName), out var cache);
            return (UserServerObject?)cache;
        }

        public static string GetCacheKey(string userName)
        {
            return $"USO::{userName}";
        }
    }

    public static class UserServerObjectExtensions
    {
        public static bool HasAccess(this UserServerObject user, ApiConfiguration apiConf)
        {
            if (user.IsPubKey) return true;

            if (apiConf.CheckAccessLevel == Common.CheckAccessLevel.OpenForAllUsers) return true;
            if (apiConf.CheckAccessLevel == Common.CheckAccessLevel.OpenForAuthenticatedUsers && !user.Id.Equals("-1")) return true;

            // check if is pubkey
            if (user.IsPubKey) return true;
            if (user.Roles is not null && user.Roles.Any(i => i.IsPubKey == true)) return true;

            // check for denied rules
            if (apiConf.DeniedUsers is not null && apiConf.DeniedUsers.Contains(user.Id)) return false;
            if (apiConf.DeniedRoles?.Count > 0 && user.Roles?.Count > 0 && apiConf.DeniedRoles.HasIntersect(user.Roles?.Select(i => i.Id).ToList())) return false;

            // check for access rules
            if (apiConf.AllowedRoles?.HasIntersect(user.Roles?.Select(i => i.Id).ToList()) == true) return true;
            if (apiConf.AllowedUsers is not null && apiConf.AllowedUsers.Contains(user.Id)) return true;

            return false;
        }

        public static string Tokenize(this UserServerObject actor)
        {
            var tokenObject = actor.ToTokenVersion();
            var jsonString = System.Text.Json.JsonSerializer.Serialize(tokenObject);
            return jsonString.EncryptAesGcm(PowNetConfiguration.EncryptionSecret);
        }

        public static UserTokenObject ToTokenVersion(this UserServerObject actor)
        {
            return new UserTokenObject
            {
                Id = actor.Id,
                UserName = actor.UserName
            };
        }

        public static UserClientObject ToClientVersion(this UserServerObject actor)
        {
            return new UserClientObject
            {
                Id = actor.Id,
                UserName = actor.UserName,
                Roles = actor.Roles,
                IsPubKey = actor.IsPubKey,
                AllowedActions = actor.AllowedActions,
                Data = actor.Data
            };
        }

        public static void ToCache(this UserServerObject actor)
        {
            actor.AddCache(UserServerObject.GetCacheKey(actor.UserName));
        }
    }

    public record UserTokenObject
    {
        public required int Id { get; set; } = 0;
        public required string UserName { get; set; }
        public DateTime GeneratedOn { get; } = DateTime.Now;
    }

    public record UserClientObject
    {
        public DateTime GeneratedOn { get; } = DateTime.Now;
        public required int Id { get; set; } = 0;
        public required string UserName { get; set; }
        public bool IsPubKey { get; set; } = false;
        public List<Role> Roles { set; get; } = [];
        public List<Action> AllowedActions { set; get; } = [];
        public JsonObject Data { set; get; } = [];
    }

    public record Role
    {
        public int Id { get; set; } = 0;
        public required string RoleName { get; set; }
        public bool IsPubKey { get; set; } = false;
        public JsonObject Data { set; get; } = [];
    }
}