using PowNet.Abstractions.Logging;
using PowNet.Configuration;
using PowNet.Extensions; // added for ToIntSafe

namespace PowNet.Implementations.Logging;

public sealed class LoggingConfigProvider : ILoggingConfigProvider, ILoggingLevelController
{
    public string? ConnectionString => PowNetConfiguration.PowNetSection["Serilog"]?["Connection"]?.ToString();
    public string ActivityTableName => PowNetConfiguration.PowNetSection["Serilog"]?["TableName"]?.ToString() ?? "Common_ActivityLog";
    public int BatchPostingLimit => (PowNetConfiguration.PowNetSection["Serilog"]?["BatchPostingLimit"]?.ToString() ?? "100").ToIntSafe();
    public int BatchPeriodSeconds => (PowNetConfiguration.PowNetSection["Serilog"]?["BatchPeriodSeconds"]?.ToString() ?? "15").ToIntSafe();
    public int RetainedFileCount => (PowNetConfiguration.PowNetSection["Serilog"]?["RetainedFileCount"]?.ToString() ?? "30").ToIntSafe();
    public int FileSizeLimitBytes => (PowNetConfiguration.PowNetSection["Serilog"]?["FileSizeLimitBytes"]?.ToString() ?? (10*1024*1024).ToString()).ToIntSafe();
    public bool BufferedFileWrite => bool.TryParse(PowNetConfiguration.PowNetSection["Serilog"]?["BufferedFileWrite"]?.ToString(), out var b) && b;
    public string LogDirectory => PowNetConfiguration.PowNetSection["Serilog"]?["LogDirectory"]?.ToString() ?? "workspace/log";

    public LogSeverity MinimumLevel { get; set; } = LogSeverity.Verbose;
}
