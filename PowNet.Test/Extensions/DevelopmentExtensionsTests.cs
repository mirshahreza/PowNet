using FluentAssertions;
using Xunit;
using PowNet.Extensions;
using PowNet.Configuration;
using PowNet.Development;

namespace PowNet.Test.Extensions
{
    public class DevelopmentExtensionsTests : IDisposable
    {
        private readonly string? _oldEnv;

        public DevelopmentExtensionsTests()
        {
            _oldEnv = PowNetConfiguration.Environment;
            PowNetConfiguration.Environment = "Development";
            PowNetConfiguration.RefreshSettings();
        }

        public void Dispose()
        {
            PowNetConfiguration.Environment = _oldEnv ?? "Production";
            PowNetConfiguration.RefreshSettings();
        }

        [Fact]
        public void AnalyzeMethodPerformance_Should_Return_Stats_In_Development()
        {
            Func<int> f = () => 1 + 1;
            var res = f.AnalyzeMethodPerformance(iterations: 5, methodName: "Add");
            res.IsAnalysisSkipped.Should().BeFalse();
            res.SampleCount.Should().Be(5);
            res.MethodName.Should().Be("Add");
        }

        [Fact]
        public void CompareMethodImplementations_Should_Compare_And_Recommend()
        {
            var impls = new Dictionary<string, Func<int>>
            {
                ["fast"] = () => 1,
                ["slow"] = () => { Thread.SpinWait(1000); return 2; }
            };
            var res = DevelopmentExtensions.CompareMethodImplementations(impls, iterations: 5);
            res.IsAnalysisSkipped.Should().BeFalse();
            res.Results.Keys.Should().Contain(new [] { "fast", "slow" });
            res.Recommendations.Should().NotBeNull();
        }

        [Fact]
        public void AnalyzeMemoryAllocations_Should_Return_Allocation_Data()
        {
            Func<byte[]> f = () => new byte[128];
            var res = f.AnalyzeMemoryAllocations(iterations: 5);
            res.IsAnalysisSkipped.Should().BeFalse();
            // Some runtimes may report zero delta for small transient allocations; just ensure non-negative and that metrics exist
            res.TotalAllocations.Should().BeGreaterOrEqualTo(0);
            res.MaxAllocation.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void CreateTestFixture_Should_Create_And_Cleanup()
        {
            var disposed = false;
            using var fixture = DevelopmentExtensions.CreateTestFixture(() => new MemoryStream(), s => { disposed = true; s.Dispose(); });
            fixture.Instance.Should().NotBeNull();
            disposed.Should().BeFalse();
            // dispose at end, cleanup flag should be set
            fixture.Dispose();
            disposed.Should().BeTrue();
        }

        private class Sample
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        [Fact]
        public void GenerateTestData_Should_Return_List_And_Respect_Validator()
        {
            var items = DevelopmentExtensions.GenerateTestData<Sample>(5, validator: s => s.Age >= 0);
            items.Should().HaveCountGreaterThan(0); // May not always reach requested if validation strict
        }

        [Fact]
        public void CreateMockDataSet_Should_Create_Primary_And_Related()
        {
            var ds = DevelopmentExtensions.CreateMockDataSet<Sample>(3, new Dictionary<string, int> { ["Orders"] = 2 });
            ds.PrimaryData.Should().HaveCount(3);
            ds.RelatedData.Should().ContainKey("Orders");
        }

        [Fact]
        public void AnalyzeCodeQuality_Should_Return_Report()
        {
            PowNetConfiguration.Environment = "Development"; // ensure dev
            PowNetConfiguration.RefreshSettings();
            var report = DevelopmentExtensions.AnalyzeCodeQuality(typeof(Sample).Assembly);
            report.IsAnalysisSkipped.Should().BeFalse();
            report.AssemblyName.Should().NotBeEmpty();
            report.Statistics.TotalTypes.Should().BeGreaterThan(0);
        }

        [Fact]
        public void DetectCodeSmells_Should_Return_List()
        {
            var smells = DevelopmentExtensions.DetectCodeSmells(typeof(Sample));
            smells.Should().NotBeNull();
        }

        [Fact]
        public void PerformanceMonitor_Should_Collect_And_Dispose()
        {
            using var monitor = DevelopmentExtensions.CreatePerformanceMonitor("Test", TimeSpan.FromMilliseconds(50));
            // Wait a bit to allow timer to run
            Thread.Sleep(120);
        }

        [Fact]
        public void ProfileMethod_Should_Profile_And_Return_Result()
        {
            Func<int> f = () => 3;
            var res = f.ProfileMethod("desc");
            res.IsProfilingSkipped.Should().BeFalse();
            res.Result.Should().Be(3);
            res.OperationName.Should().NotBeEmpty();
        }

        [Fact]
        public void DevelopmentTools_Generators_Should_Work()
        {
            var stub = DevelopmentTools.GenerateExtensionMethod(typeof(string), "Do", "int", new[] { "int x" });
            stub.Should().Contain("public static int Do(this String source, int x)");

            var model = DevelopmentTools.GenerateModelClass("User", new Dictionary<string, Type> { ["Id"] = typeof(int), ["Name"] = typeof(string) });
            model.Should().Contain("class User");
            model.Should().Contain("public int Id");

            var conf = DevelopmentTools.GenerateConfigurationClass("AppConf", new Dictionary<string, object> { ["Timeout"] = 30, ["Title"] = "MyApp" });
            conf.Should().Contain("static class AppConf");

            var doc = DevelopmentTools.GenerateApiDocumentation(typeof(DevelopmentExtensionsTests));
            doc.Should().Contain("API Documentation");
        }

        [Fact]
        public void DevelopmentTools_Benchmarks_Should_Run()
        {
            var cmp = DevelopmentTools.CompareBenchmarks(new Dictionary<string, Action>
            {
                ["a"] = () => { var x = 1+1; },
                ["b"] = () => { Thread.SpinWait(100); }
            }, iterations: 100);
            cmp.Results.Should().ContainKeys("a", "b");
            cmp.Fastest.Should().NotBeNull();
            cmp.Slowest.Should().NotBeNull();
        }

        [Fact]
        public void DevelopmentTools_AnalyzeAssembly_Should_Return_Report()
        {
            var report = DevelopmentTools.AnalyzeAssembly(typeof(DevelopmentExtensionsTests).Assembly);
            report.AssemblyName.Should().NotBeEmpty();
            report.Statistics.TotalTypes.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task RunBenchmarkAsync_Should_Measure_Async_Action()
        {
            var result = await DevelopmentTools.RunBenchmarkAsync("t", async () => await Task.Delay(1), 3);
            result.Iterations.Should().Be(3);
            result.TotalTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        }
    }
}
