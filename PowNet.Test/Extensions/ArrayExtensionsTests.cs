using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class ArrayExtensionsTests
    {
        private static byte[] CreateTestImage(int width, int height)
        {
            using var image = new Image<Rgba32>(width, height);
            using var ms = new MemoryStream();
            image.SaveAsJpeg(ms);
            return ms.ToArray();
        }

        [Theory]
        [InlineData(800, 600, 300, 300, 225)] // landscape
        [InlineData(600, 800, 300, 225, 300)] // portrait
        [InlineData(500, 500, 200, 200, 200)] // square
        public void CalculateIntelligentDimensions_Should_Preserve_Aspect_Ratio(int w, int h, int target, int expectedW, int expectedH)
        {
            // Arrange
            var oldSize = new Size(w, h);

            // Act
            var newSize = ArrayExtensions.CalculateIntelligentDimensions(oldSize, target);

            // Assert
            newSize.Width.Should().Be(expectedW);
            newSize.Height.Should().Be(expectedH);
        }

        [Theory]
        [InlineData(800, 600, 400, 400, 300)]
        [InlineData(600, 800, 300, 300, 400)]
        [InlineData(500, 500, 250, 250, 250)]
        public void CalculateIntelligentDimensionsByWidth_Should_Preserve_Aspect_Ratio(int w, int h, int targetW, int expectedW, int expectedH)
        {
            // Arrange
            var oldSize = new Size(w, h);

            // Act
            var newSize = ArrayExtensions.CalculateIntelligentDimensionsByWidth(oldSize, targetW);

            // Assert
            newSize.Width.Should().Be(expectedW);
            newSize.Height.Should().Be(expectedH);
        }

        [Theory]
        [InlineData(800, 600, 300, 400, 300)]
        [InlineData(600, 800, 400, 300, 400)]
        [InlineData(500, 500, 250, 250, 250)]
        public void CalculateIntelligentDimensionsByHeight_Should_Preserve_Aspect_Ratio(int w, int h, int targetH, int expectedW, int expectedH)
        {
            // Arrange
            var oldSize = new Size(w, h);

            // Act
            var newSize = ArrayExtensions.CalculateIntelligentDimensionsByHeight(oldSize, targetH);

            // Assert
            newSize.Width.Should().Be(expectedW);
            newSize.Height.Should().Be(expectedH);
        }

        [Theory]
        [InlineData(800, 600, 300, 300, 225)] // landscape resize image
        [InlineData(600, 800, 300, 225, 300)] // portrait resize image
        [InlineData(500, 500, 200, 200, 200)] // square resize image
        public void ResizeImage_Should_Resize_And_Preserve_Aspect_Ratio(int w, int h, int target, int expectedW, int expectedH)
        {
            // Arrange
            var imgBytes = CreateTestImage(w, h);

            // Act
            var resizedBytes = imgBytes.ResizeImage(target);

            // Assert
            using var img = Image.Load(resizedBytes);
            img.Width.Should().Be(expectedW);
            img.Height.Should().Be(expectedH);
        }

        [Fact]
        public void HasIntersect_Should_Return_False_When_Any_Null_Or_Empty()
        {
            int[]? a = null;
            int[]? b = null;
            ArrayExtensions.HasIntersect(a, b).Should().BeFalse();
            ArrayExtensions.HasIntersect(Array.Empty<int>(), new[] { 1 }).Should().BeFalse();
            ArrayExtensions.HasIntersect(new[] { 1 }, Array.Empty<int>()).Should().BeFalse();
        }

        [Fact]
        public void HasIntersect_Should_Detect_Intersection()
        {
            ArrayExtensions.HasIntersect(new[] { 1, 2, 3 }, new[] { 4, 5, 3 }).Should().BeTrue();
            ArrayExtensions.HasIntersect(new[] { 1, 2 }, new[] { 3, 4 }).Should().BeFalse();
        }
    }
}
