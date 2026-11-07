using System.Runtime.InteropServices;
using System.Text;
using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Install;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace ExileSight.Platform.Windows.Install;

/// <summary>Never follows a reparse point while classifying an entry.</summary>
public sealed class LocalPathResolver : ILocalPathResolver
{
    private readonly CheckedLocalPathResolver resolver = new(new NativeProbe());
    public bool IsRemoteDrive(string path) => resolver.IsRemoteDrive(path.Replace('/', '\\'));
    public string? Rejection(string path) => resolver.Rejection(path);
    public string? FinalPath(string path) => resolver.FinalPath(path);

    private sealed class NativeProbe : IPathComponentProbe
    {
        public bool IsRemoteDrive(string root) => DriveType(root) == 4;
        public unsafe int DriveType(string root)
        {
            fixed (char* pointer = root) return (int)PInvoke.GetDriveType(pointer);
        }

        public unsafe PathComponent Inspect(string path)
        {
            fixed (char* pointer = path)
            {
                var handle = PInvoke.CreateFile(pointer, 0,
                    FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                    null, FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                    FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS |
                    FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT, default);
                if (handle.Value == (void*)(-1))
                {
                    var error = Marshal.GetLastPInvokeError();
                    return new(error switch
                    {
                        2 or 3 => PathComponentKind.Missing,
                        15 or 21 => PathComponentKind.DriveUnavailable,
                        5 => PathComponentKind.AccessDenied,
                        _ => PathComponentKind.Rejected
                    });
                }
                try
                {
                    FILE_ATTRIBUTE_TAG_INFO info;
                    if (!PInvoke.GetFileInformationByHandleEx(handle, FILE_INFO_BY_HANDLE_CLASS.FileAttributeTagInfo,
                            &info, (uint)sizeof(FILE_ATTRIBUTE_TAG_INFO)))
                        return new(Marshal.GetLastPInvokeError() == 5 ? PathComponentKind.AccessDenied : PathComponentKind.Rejected);
                    var kind = LocalReparsePolicy.Classify((info.FileAttributes & 0x400) != 0 ? info.ReparseTag : 0, info.FileAttributes);
                    if (kind != PathComponentKind.MountPoint) return new(kind);
                    // FSCTL_GET_REPARSE_POINT reads link metadata, never its target.
                    var buffer = new byte[16384];
                    uint returned;
                    fixed (byte* output = buffer)
                    {
                        if (!PInvoke.DeviceIoControl(handle, 0x000900A8, null, 0, output,
                                (uint)buffer.Length, &returned, null))
                            return new(Marshal.GetLastPInvokeError() == 5
                                ? PathComponentKind.AccessDenied : PathComponentKind.Rejected);
                    }
                    if (returned < 8) return new(PathComponentKind.Rejected);
                    var tag = BitConverter.ToUInt32(buffer, 0);
                    if (tag != 0xA0000003 || returned < 16) return new(PathComponentKind.Rejected);
                    var offset = BitConverter.ToUInt16(buffer, 8);
                    var length = BitConverter.ToUInt16(buffer, 10);
                    if (16 + offset + length > returned || length % 2 != 0) return new(PathComponentKind.Rejected);
                    return new(PathComponentKind.MountPoint, Encoding.Unicode.GetString(buffer, 16 + offset, length));
                }
                finally { PInvoke.CloseHandle(handle); }
            }
        }
    }
}
