using Microsoft.AspNetCore.Http;
using PowNet.Abstractions.Api;
using PowNet.Abstractions.Authentication;
using PowNet.Logging;

namespace PowNet.Implementations.Api;

public sealed class ActivityLogger : IActivityLogger
{
    private readonly Logger _logger = PowNetLogger.GetLogger("Activity");

    public void LogActivity(HttpContext context, IUserIdentity user, IApiCallInfo call, string rowId, bool success, string message, int durationMs)
    {
        _logger.LogStructured(PowNet.Logging.LogLevel.Trace, "ApiCall {Controller}/{Action}", new
        {
            call.Controller,
            call.Action,
            call.Namespace,
            rowId,
            success,
            message,
            durationMs,
            user = user.UserName,
            ip = context.Connection.RemoteIpAddress?.ToString(),
            path = context.Request.Path.ToString()
        });
    }

    public void LogError(string message, Exception? ex = null)
    {
        if (ex != null) _logger.LogException(ex, message); else _logger.LogError(message);
    }

    public void LogDebug(string message) => _logger.LogDebug(message);
}
