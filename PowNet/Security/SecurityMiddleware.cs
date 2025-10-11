using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Text.Json;
using PowNet.Extensions;

namespace PowNet.Security
{
    /// <summary>
    /// Security middleware extensions for PowNet framework
    /// </summary>
    public static class SecurityMiddleware
    {
        #region Security Headers Middleware

        /// <summary>
        /// Add comprehensive security headers to HTTP response
        /// </summary>
        public static async Task AddSecurityHeaders(HttpContext context, Func<Task> next, SecurityHeadersOptions? options = null)
        {
            options ??= SecurityHeadersOptions.Default;

            // Add security headers before processing request
            var response = context.Response;

            // Content Security Policy
            if (!string.IsNullOrEmpty(options.ContentSecurityPolicy))
            {
                response.Headers.Append("Content-Security-Policy", options.ContentSecurityPolicy);
            }

            // X-Frame-Options
            response.Headers.Append("X-Frame-Options", options.XFrameOptions);

            // X-Content-Type-Options
            response.Headers.Append("X-Content-Type-Options", "nosniff");

            // X-XSS-Protection
            response.Headers.Append("X-XSS-Protection", "1; mode=block");

            // Referrer-Policy
            response.Headers.Append("Referrer-Policy", options.ReferrerPolicy);

            // Strict-Transport-Security (HSTS)
            if (context.Request.IsHttps && options.EnableHSTS)
            {
                response.Headers.Append("Strict-Transport-Security", 
                    $"max-age={options.HSTSMaxAge}; includeSubDomains{(options.HSTSPreload ? "; preload" : "")}");
            }

            // Permissions-Policy
            if (!string.IsNullOrEmpty(options.PermissionsPolicy))
            {
                response.Headers.Append("Permissions-Policy", options.PermissionsPolicy);
            }

            // Remove server information
            response.Headers.Remove("Server");
            response.Headers.Remove("X-Powered-By");
            response.Headers.Remove("X-AspNet-Version");

            await next();
        }

        #endregion

        #region Rate Limiting Middleware

        /// <summary>
        /// Rate limiting middleware with configurable policies
        /// </summary>
        public static async Task RateLimitingMiddleware(HttpContext context, Func<Task> next, RateLimitOptions? options = null)
        {
            options ??= RateLimitOptions.Default;

            var identifier = GetClientIdentifier(context, options.IdentifierStrategy);
            var endpoint = GetEndpointIdentifier(context);
            var category = $"{options.PolicyName}:{endpoint}";

            // Check if request is within rate limit
            if (!identifier.IsWithinRateLimit(options.MaxRequests, options.TimeWindow, category))
            {
                await HandleRateLimitExceeded(context, options, identifier, category);
                return;
            }

            // Record the request
            identifier.RecordRequest(category);

            // Add rate limit headers
            var remaining = identifier.GetRemainingRequests(options.MaxRequests, options.TimeWindow, category);
            context.Response.Headers.Append("X-RateLimit-Limit", options.MaxRequests.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", remaining.ToString());
            context.Response.Headers.Append("X-RateLimit-Reset", 
                DateTimeOffset.UtcNow.Add(options.TimeWindow).ToUnixTimeSeconds().ToString());

            await next();
        }

        #endregion

        #region Input Validation Middleware

        /// <summary>
        /// Comprehensive input validation middleware
        /// </summary>
        public static async Task InputValidationMiddleware(HttpContext context, Func<Task> next, ValidationOptions? options = null)
        {
            options ??= ValidationOptions.Default;

            var validationResults = new List<ValidationResult>();

            // Validate query parameters
            if (options.ValidateQueryParams)
            {
                foreach (var param in context.Request.Query)
                {
                    var result = ValidateParameter(param.Key, param.Value.ToString(), options);
                    if (!result.IsValid)
                    {
                        validationResults.Add(result);
                    }
                }
            }

            // Validate headers
            if (options.ValidateHeaders)
            {
                foreach (var header in context.Request.Headers)
                {
                    if (options.SensitiveHeaders.Contains(header.Key))
                    {
                        var result = ValidateParameter(header.Key, header.Value.ToString(), options);
                        if (!result.IsValid)
                        {
                            validationResults.Add(result);
                        }
                    }
                }
            }

            // Validate request body for POST/PUT requests
            if (options.ValidateRequestBody && 
                (context.Request.Method == "POST" || context.Request.Method == "PUT"))
            {
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrEmpty(body))
                {
                    var result = ValidateRequestBody(body, options);
                    if (!result.IsValid)
                    {
                        validationResults.Add(result);
                    }
                }
            }

            // If any validation failed, return error
            if (validationResults.Any(r => !r.IsValid))
            {
                await HandleValidationFailure(context, validationResults, options);
                return;
            }

            await next();
        }

        #endregion

        #region IP Filtering Middleware

        /// <summary>
        /// IP address filtering middleware
        /// </summary>
        public static async Task IPFilteringMiddleware(HttpContext context, Func<Task> next, IPFilterOptions? options = null)
        {
            options ??= IPFilterOptions.Default;

            var clientIP = GetClientIPAddress(context);

            // Check if IP is blocked
            if (IsIPBlocked(clientIP, options))
            {
                await HandleBlockedIP(context, clientIP, options);
                return;
            }

            // Check if IP is whitelisted (if whitelist is enabled)
            if (options.UseWhitelist && !IsIPWhitelisted(clientIP, options))
            {
                await HandleNonWhitelistedIP(context, clientIP, options);
                return;
            }

            await next();
        }

        #endregion

        #region Request Size Limiting Middleware

        /// <summary>
        /// Request size limiting middleware
        /// </summary>
        public static async Task RequestSizeLimitMiddleware(HttpContext context, Func<Task> next, RequestSizeOptions? options = null)
        {
            options ??= RequestSizeOptions.Default;

            // Check content length
            if (context.Request.ContentLength.HasValue)
            {
                if (context.Request.ContentLength.Value > options.MaxRequestSize)
                {
                    await HandleRequestTooLarge(context, options);
                    return;
                }
            }

            // Check query string length
            if (context.Request.QueryString.HasValue)
            {
                var queryLength = context.Request.QueryString.Value.Length;
                if (queryLength > options.MaxQueryStringLength)
                {
                    await HandleQueryStringTooLong(context, options);
                    return;
                }
            }

            await next();
        }

        #endregion

        #region Private Helper Methods

        private static string GetClientIdentifier(HttpContext context, ClientIdentificationStrategy strategy)
        {
            return strategy switch
            {
                ClientIdentificationStrategy.IPAddress => GetClientIPAddress(context),
                ClientIdentificationStrategy.UserAgent => context.Request.Headers["User-Agent"].ToString(),
                ClientIdentificationStrategy.Combined => $"{GetClientIPAddress(context)}:{context.Request.Headers["User-Agent"]}",
                ClientIdentificationStrategy.AuthenticatedUser => context.User.Identity?.Name ?? GetClientIPAddress(context),
                _ => GetClientIPAddress(context)
            };
        }

        private static string GetClientIPAddress(HttpContext context)
        {
            // Check for forwarded IP addresses
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var ip = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out _))
                    return ip;
            }

            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIP))
            {
                var ip = realIP.ToString().Trim();
                if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out _))
                    return ip;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private static string GetEndpointIdentifier(HttpContext context)
        {
            return $"{context.Request.Method}:{context.Request.Path}";
        }

        private static ValidationResult ValidateParameter(string name, string value, ValidationOptions options)
        {
            var issues = new List<string>();

            // SQL injection check
            if (options.CheckSqlInjection)
            {
                var sqlResult = value.ValidateSqlSafety(name);
                if (!sqlResult.IsValid)
                {
                    issues.AddRange(sqlResult.Issues);
                }
            }

            // XSS check
            if (options.CheckXSS)
            {
                var xssResult = value.ValidateXssSafety(name);
                if (!xssResult.IsValid)
                {
                    issues.AddRange(xssResult.Issues);
                }
            }

            // Length check
            if (value.Length > options.MaxParameterLength)
            {
                issues.Add($"Parameter '{name}' exceeds maximum length");
            }

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure($"Parameter validation failed for '{name}'", issues);
        }

        private static ValidationResult ValidateRequestBody(string body, ValidationOptions options)
        {
            var issues = new List<string>();

            if (body.Length > options.MaxBodySize)
            {
                issues.Add("Request body exceeds maximum size");
            }

            if (options.CheckSqlInjection)
            {
                var sqlResult = body.ValidateSqlSafety("RequestBody");
                if (!sqlResult.IsValid)
                {
                    issues.AddRange(sqlResult.Issues);
                }
            }

            if (options.CheckXSS)
            {
                var xssResult = body.ValidateXssSafety("RequestBody");
                if (!xssResult.IsValid)
                {
                    issues.AddRange(xssResult.Issues);
                }
            }

            return issues.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure("Request body validation failed", issues);
        }

        private static bool IsIPBlocked(string ipAddress, IPFilterOptions options)
        {
            if (IPAddress.TryParse(ipAddress, out var ip))
            {
                return options.BlockedIPRanges.Any(range => IsIPInRange(ip, range)) ||
                       options.BlockedIPs.Contains(ipAddress);
            }
            return false;
        }

        private static bool IsIPWhitelisted(string ipAddress, IPFilterOptions options)
        {
            if (IPAddress.TryParse(ipAddress, out var ip))
            {
                return options.WhitelistedIPRanges.Any(range => IsIPInRange(ip, range)) ||
                       options.WhitelistedIPs.Contains(ipAddress);
            }
            return false;
        }

        private static bool IsIPInRange(IPAddress ipAddress, IPRange range)
        {
            var ipBytes = ipAddress.GetAddressBytes();
            var startBytes = range.Start.GetAddressBytes();
            var endBytes = range.End.GetAddressBytes();

            if (ipBytes.Length != startBytes.Length || ipBytes.Length != endBytes.Length)
                return false;

            for (int i = 0; i < ipBytes.Length; i++)
            {
                if (ipBytes[i] < startBytes[i] || ipBytes[i] > endBytes[i])
                    return false;
            }

            return true;
        }

        #endregion

        #region Error Handlers

        private static async Task HandleRateLimitExceeded(HttpContext context, RateLimitOptions options, 
            string identifier, string category)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers.Append("Retry-After", options.TimeWindow.TotalSeconds.ToString());

            var response = new
            {
                error = "Rate limit exceeded",
                message = options.ErrorMessage,
                retryAfter = options.TimeWindow.TotalSeconds
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));

            // Log rate limit violation
            LogSecurityEvent("RateLimitExceeded", new Dictionary<string, object>
            {
                ["Identifier"] = identifier,
                ["Category"] = category,
                ["IPAddress"] = GetClientIPAddress(context),
                ["UserAgent"] = context.Request.Headers["User-Agent"].ToString(),
                ["Endpoint"] = GetEndpointIdentifier(context)
            });
        }

        private static async Task HandleValidationFailure(HttpContext context, List<ValidationResult> results, 
            ValidationOptions options)
        {
            context.Response.StatusCode = 400; // Bad Request

            var response = new
            {
                error = "Validation failed",
                message = options.ErrorMessage,
                details = results.Where(r => !r.IsValid).Select(r => new
                {
                    message = r.ErrorMessage,
                    issues = r.Issues
                })
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));

            // Log validation failure
            LogSecurityEvent("ValidationFailure", new Dictionary<string, object>
            {
                ["IPAddress"] = GetClientIPAddress(context),
                ["UserAgent"] = context.Request.Headers["User-Agent"].ToString(),
                ["Endpoint"] = GetEndpointIdentifier(context),
                ["ValidationErrors"] = results.Count
            });
        }

        private static async Task HandleBlockedIP(HttpContext context, string ipAddress, IPFilterOptions options)
        {
            context.Response.StatusCode = 403; // Forbidden

            var response = new
            {
                error = "Access denied",
                message = options.BlockedMessage
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));

            // Log blocked IP access attempt
            LogSecurityEvent("BlockedIPAccess", new Dictionary<string, object>
            {
                ["IPAddress"] = ipAddress,
                ["UserAgent"] = context.Request.Headers["User-Agent"].ToString(),
                ["Endpoint"] = GetEndpointIdentifier(context)
            });
        }

        private static async Task HandleNonWhitelistedIP(HttpContext context, string ipAddress, IPFilterOptions options)
        {
            context.Response.StatusCode = 403; // Forbidden

            var response = new
            {
                error = "Access denied",
                message = options.NonWhitelistedMessage
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));

            // Log non-whitelisted IP access attempt
            LogSecurityEvent("NonWhitelistedIPAccess", new Dictionary<string, object>
            {
                ["IPAddress"] = ipAddress,
                ["UserAgent"] = context.Request.Headers["User-Agent"].ToString(),
                ["Endpoint"] = GetEndpointIdentifier(context)
            });
        }

        private static async Task HandleRequestTooLarge(HttpContext context, RequestSizeOptions options)
        {
            context.Response.StatusCode = 413; // Payload Too Large

            var response = new
            {
                error = "Request too large",
                message = options.ErrorMessage,
                maxSize = options.MaxRequestSize
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }

        private static async Task HandleQueryStringTooLong(HttpContext context, RequestSizeOptions options)
        {
            context.Response.StatusCode = 414; // URI Too Long

            var response = new
            {
                error = "Query string too long",
                message = "Query string exceeds maximum allowed length",
                maxLength = options.MaxQueryStringLength
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }

        private static void LogSecurityEvent(string eventType, Dictionary<string, object> data)
        {
            // In a real implementation, this would integrate with your logging framework
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Data = data
            };

            // For now, just write to debug output
            System.Diagnostics.Debug.WriteLine($"Security Event: {System.Text.Json.JsonSerializer.Serialize(logEntry)}");
        }

        #endregion
    }

    #region Configuration Classes

    /// <summary>
    /// Security headers configuration options
    /// </summary>
    public class SecurityHeadersOptions
    {
        public string ContentSecurityPolicy { get; set; } = string.Empty;
        public string XFrameOptions { get; set; } = "DENY";
        public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
        public bool EnableHSTS { get; set; } = true;
        public int HSTSMaxAge { get; set; } = 31536000; // 1 year
        public bool HSTSPreload { get; set; } = false;
        public string PermissionsPolicy { get; set; } = string.Empty;

        public static SecurityHeadersOptions Default => new()
        {
            ContentSecurityPolicy = AuthExtensions.GenerateCSPHeader(),
            EnableHSTS = true
        };
    }

    /// <summary>
    /// Rate limiting configuration options
    /// </summary>
    public class RateLimitOptions
    {
        public string PolicyName { get; set; } = "Default";
        public int MaxRequests { get; set; } = 100;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);
        public ClientIdentificationStrategy IdentifierStrategy { get; set; } = ClientIdentificationStrategy.IPAddress;
        public string ErrorMessage { get; set; } = "Rate limit exceeded. Please try again later.";

        public static RateLimitOptions Default => new();
    }

    /// <summary>
    /// Input validation configuration options
    /// </summary>
    public class ValidationOptions
    {
        public bool ValidateQueryParams { get; set; } = true;
        public bool ValidateHeaders { get; set; } = true;
        public bool ValidateRequestBody { get; set; } = true;
        public bool CheckSqlInjection { get; set; } = true;
        public bool CheckXSS { get; set; } = true;
        public int MaxParameterLength { get; set; } = 4000;
        public int MaxBodySize { get; set; } = 1024 * 1024; // 1MB
        public HashSet<string> SensitiveHeaders { get; set; } = new() { "Authorization", "Cookie" };
        public string ErrorMessage { get; set; } = "Invalid input detected.";

        public static ValidationOptions Default => new();
    }

    /// <summary>
    /// IP filtering configuration options
    /// </summary>
    public class IPFilterOptions
    {
        public bool UseWhitelist { get; set; } = false;
        public HashSet<string> WhitelistedIPs { get; set; } = new();
        public List<IPRange> WhitelistedIPRanges { get; set; } = new();
        public HashSet<string> BlockedIPs { get; set; } = new();
        public List<IPRange> BlockedIPRanges { get; set; } = new();
        public string BlockedMessage { get; set; } = "Your IP address has been blocked.";
        public string NonWhitelistedMessage { get; set; } = "Access denied.";

        public static IPFilterOptions Default => new();
    }

    /// <summary>
    /// Request size limiting configuration options
    /// </summary>
    public class RequestSizeOptions
    {
        public long MaxRequestSize { get; set; } = 30 * 1024 * 1024; // 30MB
        public int MaxQueryStringLength { get; set; } = 2048;
        public string ErrorMessage { get; set; } = "Request size exceeds the allowed limit.";

        public static RequestSizeOptions Default => new();
    }

    /// <summary>
    /// Client identification strategies for rate limiting
    /// </summary>
    public enum ClientIdentificationStrategy
    {
        IPAddress,
        UserAgent,
        Combined,
        AuthenticatedUser
    }

    /// <summary>
    /// IP address range for filtering
    /// </summary>
    public class IPRange
    {
        public IPAddress Start { get; set; } = IPAddress.None;
        public IPAddress End { get; set; } = IPAddress.None;

        public IPRange(string startIP, string endIP)
        {
            Start = IPAddress.Parse(startIP);
            End = IPAddress.Parse(endIP);
        }

        public IPRange(IPAddress start, IPAddress end)
        {
            Start = start;
            End = end;
        }
    }

    #endregion
}