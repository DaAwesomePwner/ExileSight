using ExileSight.Platform.Abstractions.Install;

namespace ExileSight.Core.Setup;

/// <summary>Checks each component before traversing it. Missing descendants remain local and retryable.
/// Symlinks are rejected; mount-point target text is checked recursively before opening its target.
/// Concurrent hostile filesystem replacement is outside this metadata-check contract.</summary>
public sealed class CheckedLocalPathResolver(IPathComponentProbe probe) : ILocalPathResolver
{
    public bool IsRemoteDrive(string path) => Root(path) is not { } root || probe.IsRemoteDrive(root);

    public string? FinalPath(string path) => Check(path.Replace('/', '\\'), 0) ? path : null;

    private bool Check(string path, int depth)
    {
        if (depth > 32 || Root(path) is not { } root || probe.IsRemoteDrive(root)) return false;
        var components = path[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (components.Any(p => p is "." or ".." || p.Contains(':'))) return false;
        var current = root;
        for (var i = 0; i < components.Length; i++)
        {
            current = current.TrimEnd('\\') + "\\" + components[i];
            var entry = probe.Inspect(current);
            switch (entry.Kind)
            {
                case PathComponentKind.Missing: return true; // Nearest existing ancestors passed.
                case PathComponentKind.SymbolicLink:
                case PathComponentKind.Rejected: return false;
                case PathComponentKind.MountPoint:
                    var target = entry.Target;
                    if (target is null) return false;
                    // NT substitute names are data, never opened before root classification.
                    if (target.StartsWith(@"\??\", StringComparison.Ordinal)) target = target[4..];
                    if (target.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)) target = @"\\?\" + target;
                    var rest = string.Join("\\", components.Skip(i + 1));
                    return Check(target.TrimEnd('\\') + (rest.Length == 0 ? "\\" : "\\" + rest), depth + 1);
            }
        }
        return true;
    }

    private static string? Root(string path)
    {
        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\')
            return path[..3];
        const string volumePrefix = @"\\?\Volume{";
        if (path.StartsWith(volumePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var end = path.IndexOf("}\\", StringComparison.Ordinal);
            if (end > 0 && Guid.TryParse(path[volumePrefix.Length..end], out _)) return path[..(end + 2)];
        }
        return null;
    }
}
