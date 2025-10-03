using FluentAssertions;
using Xunit;
using PowNet.Development;

namespace PowNet.Test.Development
{
    public class DevelopmentToolsTests
    {
        [Fact]
        public void Generate_Code_Stubs_Should_Work()
        {
            var ext = DevelopmentTools.GenerateExtensionMethod(typeof(string), "X", "int", new[]{"int a"});
            ext.Should().Contain("static class StringExtensions");

            var model = DevelopmentTools.GenerateModelClass("M", new Dictionary<string, Type>{{"A", typeof(int)}}, "Ns");
            model.Should().Contain("namespace Ns");

            var conf = DevelopmentTools.GenerateConfigurationClass("MyConf", new Dictionary<string, object>{{"A", 1}, {"B", "x"}});
            conf.Should().Contain("public static class MyConf");

            var doc = DevelopmentTools.GenerateApiDocumentation(typeof(DevelopmentToolsTests));
            doc.Should().Contain("API Documentation");
        }

        [Fact]
        public async Task Benchmark_Should_Run()
        {
            var result = DevelopmentTools.RunBenchmark("inc", () => { var x=0; x++; }, 10);
            result.Iterations.Should().Be(10);
            var resultA = await DevelopmentTools.RunBenchmarkAsync("aw", async () => { await Task.Delay(1); }, 3);
            resultA.Iterations.Should().Be(3);

            var comp = DevelopmentTools.CompareBenchmarks(new Dictionary<string, Action>{
                ["a"] = () => { },
                ["b"] = () => { }
            }, 10);
            comp.Results.Count.Should().Be(2);
        }

        [Fact]
        public void Dev_Info_And_Analysis_Should_Work()
        {
            var info = DevelopmentTools.GetDevelopmentInfo();
            info.Should().NotBeNull();

            var report = DevelopmentTools.AnalyzeAssembly(typeof(DevelopmentToolsTests).Assembly);
            report.Statistics.TotalTypes.Should().BeGreaterOrEqualTo(0);

            var dt = DevelopmentTools.GenerateTestData<DateTime>();
            (dt is DateTime).Should().BeTrue();

            var list = DevelopmentTools.GenerateTestDataCollection<object>(3);
            list.Count.Should().Be(3);
        }
    }
}
