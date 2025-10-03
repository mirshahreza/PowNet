using FluentAssertions;
using Xunit;
using PowNet.Diagnostics;

namespace PowNet.Test.Diagnostics
{
    public class DiagnosticsManagerTests
    {
        [Fact]
        public void Measure_And_Stats_Should_Work()
        {
            using (DiagnosticsManager.MeasurePerformance("op1"))
            {
                Thread.Sleep(5);
            }
            var stats = DiagnosticsManager.GetPerformanceStatistics("op1");
            stats.Should().NotBeNull();
            var all = DiagnosticsManager.GetAllPerformanceStatistics();
            all.Should().ContainKey("op1");
        }

        [Fact]
        public void Memory_Diagnostics_And_GC_Should_Work()
        {
            var md = DiagnosticsManager.GetMemoryDiagnostics();
            md.TotalMemory.Should().BeGreaterOrEqualTo(0);
            var rep = DiagnosticsManager.ForceGarbageCollection();
            rep.MemoryBefore.Should().BeGreaterOrEqualTo(rep.MemoryAfter);
        }

        [Fact]
        public void Health_Report_Should_Contain_Data()
        {
            var hr = DiagnosticsManager.GetHealthReport();
            hr.ProcessInfo.ProcessId.Should().BeGreaterThan(0);
            hr.EnvironmentInfo.MachineName.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ExecuteInDevelopment_Should_NotThrow()
        {
            DiagnosticsManager.ExecuteInDevelopment(() => { var x = 1+1; });
            var r = DiagnosticsManager.ExecuteInDevelopment(() => 42, -1);
            r.Should().BeOneOf(42, -1);
        }

        [Fact]
        public void DebugAssert_And_DumpObject_And_CallStack_Should_Work()
        {
            DiagnosticsManager.DebugAssert(true, "ok");
            DiagnosticsManager.DumpObject(new { A = 1 }, "obj");
            var cs = DiagnosticsManager.GetCallStack();
            cs.TotalFrames.Should().BeGreaterOrEqualTo(0);
        }
    }
}
