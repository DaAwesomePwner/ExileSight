using ExileSight.Platform.Abstractions.Install;

namespace ExileSight.Core.Setup;

public static class LocalReparsePolicy
{
    // Microsoft MS-FSCC reparse tags; FILE_ATTRIBUTE_OFFLINE/RECALL_ON_OPEN/RECALL_ON_DATA_ACCESS.
    // https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-tags
    public static PathComponentKind Classify(uint tag, uint attributes)
    {
        if ((attributes & (0x1000u | 0x40000u | 0x400000u)) != 0)
            return PathComponentKind.CloudPlaceholder;
        if (tag is 0 or 0x80000013) return PathComponentKind.Ordinary; // Dedup is local storage.
        if ((tag & 0xFFFF0FFFu) == 0x9000001A) return PathComponentKind.Ordinary; // Hydrated cloud variants.
        return tag switch
        {
            0xA000000C => PathComponentKind.SymbolicLink,
            0xA0000003 => PathComponentKind.MountPoint,
            _ => PathComponentKind.Rejected
        };
    }
}
