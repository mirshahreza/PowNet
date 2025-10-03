using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PowNet.Configuration;
using PowNet.Models;
using PowNet.Extensions;
using PowNet.Common;

namespace PowNet.Services
{
    public record ApiCallInfo(string RequestPath, string NamespaceName, string ControllerName, string ApiName)
    {
        public string RequestPath { get; set; } = RequestPath;
        public string NamespaceName { get; set; } = NamespaceName;
        public string ControllerName { get; set; } = ControllerName;
        public string ApiName { get; set; } = ApiName;
    }

    public static class ApiCallInfoExtensions
    {
        public static string GetCacheKey(this ApiCallInfo apiInfo, ApiConfiguration apiConf, UserServerObject uso)
        {
            return $"Response::{apiInfo.ControllerName}_{apiInfo.ApiName}{(apiConf.CacheLevel == CacheLevel.PerUser ? "_" + uso.UserName : "")}";
        }

        public static ControllerConfiguration GetConfig(this ApiCallInfo apiInfo)
        {
            return ControllerConfiguration.GetConfig(apiInfo.NamespaceName, apiInfo.ControllerName);
        }

        public static ApiCallInfo GetAppEndWebApiInfo(this HttpContext context)
        {
            try
            {
                var routeData = context.GetRouteData();
                string path = context.Request.Path.ToString();
                string controllerName = routeData.Values["controller"].ToStringEmpty();
                string actionName = routeData.Values["action"].ToStringEmpty();
                string namespaceName = path.Replace(controllerName, "").Replace(actionName, "").Replace("/", "");
                return new ApiCallInfo(path, namespaceName, controllerName, actionName);
            }
            catch
            {
                throw new Exception($"RequestedPathIsNotValid [{context.Request.Path}]");
            }
        }
    }
}