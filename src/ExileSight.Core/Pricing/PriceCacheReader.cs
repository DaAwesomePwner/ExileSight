namespace ExileSight.Core.Pricing;

/// <summary>Read-only seam for transient filesystem failures; writes still use RootedFiles.</summary>
internal interface IPriceCacheReader
{
    byte[] Read(string path);
    DateTimeOffset LastWriteTime(string path);
}

internal sealed class PriceCacheReader : IPriceCacheReader
{
    public byte[] Read(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new IOException("Price cache too large.");
        return File.ReadAllBytes(path);
    }

    public DateTimeOffset LastWriteTime(string path) => new(File.GetLastWriteTimeUtc(path));
}
