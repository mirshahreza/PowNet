namespace PowNet.Abstractions.Logging
{
    public enum LogSeverity
    {
        Verbose = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5
    }

    public interface ILoggingConfigProvider
    {
        string? ConnectionString { get; }
        string ActivityTableName { get; }
        int BatchPostingLimit { get; }
        int BatchPeriodSeconds { get; }
        int RetainedFileCount { get; }
        int FileSizeLimitBytes { get; }
        bool BufferedFileWrite { get; }
        string LogDirectory { get; }
    }

    public interface ILoggingLevelController
    {
        LogSeverity MinimumLevel { get; set; }
    }
}
