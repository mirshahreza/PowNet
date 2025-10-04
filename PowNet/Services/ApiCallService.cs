using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PowNet.Configuration;
using PowNet.Models;
using PowNet.Extensions;
using PowNet.Common;
using System;

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
                return new ApiCallInfo(path, namespaceName, controllerName, actionName);
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