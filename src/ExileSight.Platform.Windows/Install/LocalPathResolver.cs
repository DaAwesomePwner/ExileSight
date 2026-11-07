using ExileSight.Platform.Abstractions.Install;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace ExileSight.Platform.Windows.Install;

/// <summary>Checks the volume and follows all reparse points using a metadata-only file handle.</summary>
public sealed class LocalPathResolver : ILocalPathResolver
{
    public unsafe bool IsRemoteDrive(string path)
    {
        var root = Path.GetPathRoot(path);
        fixed (char* pointer = root) return PInvoke.GetDriveType(pointer) == 4;
    }

    public unsafe string? FinalPath(string path)
    {
        fixed (char* pointer = path)
        {
            var handle = PInvoke.CreateFile(pointer, 0,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null, FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS, default);
            if (handle.Value == (void*)(-1)) return null;
            try
            {
                var buffer = new char[32768];
                fixed (char* output = buffer)
                {
                    var length = PInvoke.GetFinalPathNameByHandle(handle, output, (uint)buffer.Length, 0);
                    return length == 0 || length >= buffer.Length ? null : new string(output, 0, (int)length);
                }
            }
            finally { PInvoke.CloseHandle(handle); }
        }
    }
}
