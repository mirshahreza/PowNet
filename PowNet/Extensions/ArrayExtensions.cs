using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PowNet.Extensions
{
    public static class ArrayExtensions
    {
        public static byte[] ResizeImage(this byte[] imageFile, int targetSize)
        {
            using Image image = Image.Load(imageFile);
            var ms = new MemoryStream();
            Size newSize = CalculateIntelligentDimensions(image.Size, targetSize);
            int width = newSize.Width;
            int height = newSize.Height;
            image.Mutate(x => x.Resize(width, height));
            image.Save(ms, new JpegEncoder { Quality = 80 });
            return ms.ToArray();
        }

        public static Size CalculateIntelligentDimensions(Size oldSize, int targetSize)
        {
            Size newSize = new();
            if (oldSize.Height > oldSize.Width)
            {
                newSize.Width = (int)(oldSize.Width * (targetSize / (float)oldSize.Height));
                newSize.Height = targetSize;
            }
            else
            {
                newSize.Width = targetSize;
                newSize.Height = (int)(oldSize.Height * (targetSize / (float)oldSize.Width));
            }
            return newSize;
        }

        public static Size CalculateIntelligentDimensionsByWidth(Size oldSize, int targetWidth)
        {
            float resizeRatio = (float)targetWidth / (float)oldSize.Width;
            return new Size(targetWidth, (int)(oldSize.Height * resizeRatio));
        }

        public static Size CalculateIntelligentDimensionsByHeight(Size oldSize, int targetHeight)
        {
            float resizeRatio = (float)targetHeight / (float)oldSize.Height;
            return new Size((int)(oldSize.Width * resizeRatio), targetHeight);
        }

        public static bool HasIntersect(this int[]? l1, int[]? l2)
        {
            if (l2 is null || l2.Length == 0) return false;
            if (l1 is null || l1.Length == 0) return false;
            foreach (int i in l1) if (l2.Contains(i)) return true;
            return false;
        }
    }
}