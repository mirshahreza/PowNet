using System.Data;
using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class ArrayAndDataTableExtensionsTests
    {
        [Fact]
        public void ArrayExtensions_HasIntersect_And_Dimension_Calculations_Should_Work()
        {
            var has = new[]{1,2,3}.HasIntersect(new[]{3,4});
            has.Should().BeTrue();

            var s = ArrayExtensions.CalculateIntelligentDimensions(new SixLabors.ImageSharp.Size(200,100), 50);
            s.Width.Should().Be(50);
            s.Height.Should().Be(25);
            var s2 = ArrayExtensions.CalculateIntelligentDimensionsByWidth(new SixLabors.ImageSharp.Size(200,100), 100);
            s2.Height.Should().Be(50);
            var s3 = ArrayExtensions.CalculateIntelligentDimensionsByHeight(new SixLabors.ImageSharp.Size(200,100), 50);
            s3.Width.Should().Be(100);
        }

        [Fact]
        public void DataTableExtensions_ToCSV_Should_Run()
        {
            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Blob", typeof(byte[]));
            dt.Rows.Add(1, "A", new byte[]{1,2});
            dt.Rows.Add(2, "B", new byte[]{3,4});

            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            dt.ToCSV(tmp, exceptColumns: new List<string>{"Name"});
            Directory.GetFiles(tmp, "*.csv").Length.Should().Be(1);
            Directory.Delete(tmp, true);
        }
    }
}
