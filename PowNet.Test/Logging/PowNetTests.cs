using FluentAssertions;
using Xunit;
using PowNet.Logging;
using PowNet.Configuration;

namespace PowNet.Test.Logging
{
    public class PowNetLoggerTests
    {
        public PowNetLoggerTests()
        {
            PowNetConfiguration.Environment = "Development";
            PowNetConfiguration.RefreshSettings();
            PowNetLogger.Initialize();
        }

        [Fact]
        public void Logger_Should_Log_Basic_Messages_Without_Exception()
        {
            var logger = PowNetLogger.GetLogger("Test");
            logger.LogInformation("Hello {0}", "World");
            logger.LogDebug("Named template {Name}", "Alice"); // SafeFormat protects this
            logger.LogWarning("Warn {0}", 1);
        }

        [Fact]
        public void Logger_Structured_And_Scoped_Should_Work()
        {
            var logger = PowNetLogger.GetLogger("Test2");
            logger.LogStructured(LogLevel.Information, "User logged in", new { UserId = 1, Name = "A" });
            using (logger.BeginScope("Op", new { Id = 1 }))
            {
                // scope start/end messages are written via LogScope
            }
        }

        [Fact]
        public void Logger_Performance_APIs_Should_Work()
        {
            var logger = PowNetLogger.GetLogger("Perf");
            var res = logger.MeasurePerformance("calc", () => 2 + 2);
            res.Should().Be(4);
            var res2 = logger.MeasurePerformanceAsync("calcA", async () => { await Task.Delay(10); return 3; }).Result;
            res2.Should().Be(3);
        }

        [Fact]
        public void LoggingExtensions_Should_Log_Common_Scenarios()
        {
            var logger = PowNetLogger.GetLogger("Ext");
            logger.LogMethodEntry(new { A = 1 });
            logger.LogHttpRequest("GET", "/path", 200, TimeSpan.FromMilliseconds(5));
            logger.LogDatabaseOperation("SELECT", "select 1", TimeSpan.FromMilliseconds(1), 1);
            logger.LogBusinessOperation("CreateOrder", true, new { Id = 10 });
            logger.LogMethodExit("ok");
        }
    }
}
