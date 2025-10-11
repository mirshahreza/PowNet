using System.Reflection;

namespace PowNet.Abstractions.Plugins
{
    public readonly record struct PluginLoadResult(bool Success, string Path, Assembly? Assembly, TimeSpan Duration, Exception? Error, bool IsDynamic, long FileSizeBytes)
    {
        public static PluginLoadResult Successful(string path, Assembly asm, TimeSpan dur, bool isDynamic, long size) => new(true, path, asm, dur, null, isDynamic, size);
        public static PluginLoadResult Failed(string path, Exception ex) => new(false, path, null, TimeSpan.Zero, ex, false, 0);
    }

    public readonly record struct PluginUnloadResult(bool Success, string Path, TimeSpan Duration, Exception? Error, int GcLoops, bool ContextStillAlive)
    {
        public static PluginUnloadResult Successful(string path, TimeSpan dur, int loops) => new(true, path, dur, null, loops, false);
        public static PluginUnloadResult Failed(string path, Exception ex, TimeSpan? dur = null, int loops = 0, bool alive = false) => new(false, path, dur ?? TimeSpan.Zero, ex, loops, alive);
    }

    public interface IPluginHandle
    {
        string Path { get; }
        Assembly Assembly { get; }
        DateTime LoadedUtc { get; }
        bool IsDynamic { get; }
        long FileSizeBytes { get; }
    }

    public interface IPluginManager
    {
        PluginLoadResult Load(string dllFullPath);
        PluginLoadResult LoadDynamic();
        PluginUnloadResult Unload(string dllFullPath);
        IReadOnlyCollection<IPluginHandle> GetLoaded();
    }
}
