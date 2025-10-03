using FluentAssertions;
using Xunit;
using PowNet.Extensions;

namespace PowNet.Test.Extensions
{
    public class IOExtensionsTests
    {
        [Fact]
        public void CopyFilesRecursively_Should_Copy_All_Files()
        {
            var src = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var dst = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            try
            {
                src.Create();
                Directory.CreateDirectory(Path.Combine(src.FullName, "sub"));
                File.WriteAllText(Path.Combine(src.FullName, "a.txt"), "A");
                File.WriteAllText(Path.Combine(src.FullName, "sub", "b.txt"), "B");

                src.CopyFilesRecursively(dst);

                File.Exists(Path.Combine(dst.FullName, "a.txt")).Should().BeTrue();
                File.Exists(Path.Combine(dst.FullName, "sub", "b.txt")).Should().BeTrue();
            }
            finally
            {
                if (src.Exists) src.Delete(true);
                if (dst.Exists) dst.Delete(true);
            }
        }

        [Fact]
        public void GetFilesRecursive_Should_Return_All_Files_And_Handle_Errors()
        {
            var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            try
            {
                dir.Create();
                Directory.CreateDirectory(Path.Combine(dir.FullName, "x"));
                File.WriteAllText(Path.Combine(dir.FullName, "a.txt"), "A");
                File.WriteAllText(Path.Combine(dir.FullName, "x", "b.log"), "B");

                var files = dir.GetFilesRecursive().ToList();
                files.Should().Contain(f => f.EndsWith("a.txt") && f.Contains(dir.FullName));
                files.Should().Contain(f => f.EndsWith("b.log"));

                var txtOnly = dir.GetFilesRecursive("*.txt").ToList();
                txtOnly.Should().ContainSingle(f => f.EndsWith("a.txt"));
            }
            finally
            {
                if (dir.Exists) dir.Delete(true);
            }
        }

        [Fact]
        public void GetFilesRecursiveWithInfo_Should_Return_Dictionary_Of_Metadata()
        {
            var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            try
            {
                dir.Create();
                var file = Path.Combine(dir.FullName, "a.txt");
                File.WriteAllText(file, "A");

                var dict = dir.GetFilesRecursiveWithInfo();
                dict.Should().ContainKey(file);
                dict[file]["FileName"].Should().Be("a.txt");
                dict[file]["FilePath"].Should().Be(file);
                dict[file].Should().ContainKey("LastWrite");
            }
            finally
            {
                if (dir.Exists) dir.Delete(true);
            }
        }

        [Fact]
        public void Delete_Should_Remove_Files_By_SearchPattern()
        {
            var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            try
            {
                dir.Create();
                File.WriteAllText(Path.Combine(dir.FullName, "a.txt"), "A");
                File.WriteAllText(Path.Combine(dir.FullName, "b.log"), "B");
                dir.Delete("*.txt");
                File.Exists(Path.Combine(dir.FullName, "a.txt")).Should().BeFalse();
                File.Exists(Path.Combine(dir.FullName, "b.log")).Should().BeTrue();
            }
            finally
            {
                if (dir.Exists) dir.Delete(true);
            }
        }

        [Fact]
        public void GetCacheKeyForFiles_Should_Prefix_With_file_DoubleColon()
        {
            var file = new FileInfo(Path.GetTempPath());
            var key = file.GetCacheKeyForFiles();
            key.Should().StartWith("file::");
        }

        [Fact]
        public void TryDelete_Should_Return_True_For_Existing_And_Missing_File()
        {
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(tmp, "x");
            IOExtensions.TryDelete(tmp).Should().BeTrue();
            // Deleting again should be a no-op and still return true since File.Delete does not throw if file is missing
            IOExtensions.TryDelete(tmp).Should().BeTrue();
            File.Exists(tmp).Should().BeFalse();
        }

        [Fact]
        public void Path_Checks_Should_Work()
        {
            var dir = Path.GetTempPath();
            IOExtensions.IsFolder(dir).Should().BeTrue();
            IOExtensions.IsFile(dir).Should().BeFalse();

            var file = Path.GetTempFileName();
            IOExtensions.IsFile(file).Should().BeTrue();
        }
    }
}
