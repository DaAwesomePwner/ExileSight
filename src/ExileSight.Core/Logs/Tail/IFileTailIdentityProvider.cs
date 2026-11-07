namespace ExileSight.Core.Logs.Tail;

/// <summary>Identity metadata must describe the open handle, not its possibly rotated path.</summary>
public interface IFileTailIdentityProvider
{
    FileTailIdentity Read(FileStream file);
}

public readonly record struct FileTailIdentity(DateTime CreationUtc, bool HasStableCreationTime);

internal sealed class HandleIdentityProvider : IFileTailIdentityProvider
{
    public FileTailIdentity Read(FileStream file)
    {
        var creation = File.GetCreationTimeUtc(file.SafeFileHandle);
        var modified = File.GetLastWriteTimeUtc(file.SafeFileHandle);
        // Some filesystems synthesize creation time from modification time. Treat equality
        // conservatively, including a freshly created file, and use content checkpoints instead.
        return new(creation, creation > DateTime.UnixEpoch && creation != modified);
    }
}
