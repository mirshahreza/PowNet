using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PowNet.Configuration;
using PowNet.Models;
using PowNet.Extensions;
using PowNet.Common;
using PowNet.Abstractions.Api; // added

namespace PowNet.Services
{
    public record ApiCallInfo(string RequestPath, string NamespaceName, string ControllerName, string ApiName) : IApiCallInfo
    {
        public string RequestPath { get; set; } = RequestPath;
        public string NamespaceName { get; set; } = NamespaceName;
        public string ControllerName { get; set; } = ControllerName;
        public string ApiName { get; set; } = ApiName;

        // IApiCallInfo members mapping
        string? IApiCallInfo.Namespace => NamespaceName;
        string? IApiCallInfo.Controller => ControllerName;
        string? IApiCallInfo.Action => ApiName;
        IReadOnlyDictionary<string, string?> IApiCallInfo.RouteValues => _routeValues;
        private readonly Dictionary<string,string?> _routeValues = new();

        public void AddRouteValue(string key, string? value)
        {
            _routeValues[key] = value;
        }
    }

    public static class ApiCallInfoExtensions
    {
        public static string GetCacheKey(this ApiCallInfo apiInfo, ApiConfiguration apiConf, UserServerObject uso)
        {
            return $"Response::{apiInfo.ControllerName}_{apiInfo.ApiName}{(apiConf.CacheLevel == CacheLevel.PerUser ? "_" + uso.UserName : string.Empty)}";
        }

        public static ControllerConfiguration GetConfig(this ApiCallInfo apiInfo)
        {
            return ControllerConfiguration.GetConfig(apiInfo.NamespaceName, apiInfo.ControllerName);
        }

        /// <summary>
        /// Build an ApiCallInfo from the current HttpContext route values.
        /// </summary>
        public static ApiCallInfo GetApiCallInfo(this HttpContext context)
        {
            try
            {
                var routeData = context.GetRouteData();
                string path = context.Request.Path.ToString();
                string controllerName = routeData.Values["controller"].ToStringEmpty();
                string actionName = routeData.Values["action"].ToStringEmpty();
                string namespaceName = path.Replace(controllerName, string.Empty).Replace(actionName, string.Empty).Replace("/", string.Empty);
                var info = new ApiCallInfo(path, namespaceName, controllerName, actionName);
                foreach (var kv in routeData.Values)
                {
                    info.AddRouteValue(kv.Key, kv.Value?.ToString());
                }
                return info;
            }
            catch
            {
                throw new Exception($"RequestedPathIsNotValid [{context.Request.Path}]");
            }
        }

        /// <summary>
        /// Obsolete: use GetApiCallInfo().
        /// </summary>
        [Obsolete("Use GetApiCallInfo() instead.")]
        public static ApiCallInfo GetAppEndWebApiInfo(this HttpContext context) => GetApiCallInfo(context);
    }
}