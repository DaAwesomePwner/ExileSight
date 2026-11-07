using ExileSight.Platform.Abstractions.Install;

namespace ExileSight.Core.Setup;

/// <summary>Checks each component before traversing it. Missing descendants remain local and retryable.
/// Symlinks are rejected; mount-point target text is checked recursively before opening its target.
/// Concurrent hostile filesystem replacement is outside this metadata-check contract.</summary>
public sealed class CheckedLocalPathResolver(IPathComponentProbe probe) : ILocalPathResolver
{
    public bool IsRemoteDrive(string path) => Root(path) is not { } root || probe.IsRemoteDrive(root);

    public string? FinalPath(string path) => Rejection(path) is null ? path : null;
    public string? Rejection(string path) => Check(path.Replace('/', '\\'), 0);

    private string? Check(string path, int depth)
    {
        if (depth > 32) return "Too many local path redirects.";
        if (Root(path) is not { } root) return "Choose a fully-qualified local path; network paths are not allowed.";
        var driveType = probe.DriveType(root);
        if (driveType is 0 or 1) return "Drive not available; retrying.";
        if (driveType == 4) return "Network paths are not allowed.";
        var components = path[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (components.Any(p => p is "." or ".." || p.Contains(':'))) return "Unsupported local path.";
        var current = root;
        for (var i = 0; i < components.Length; i++)
        {
            current = current.TrimEnd('\\') + "\\" + components[i];
            var entry = probe.Inspect(current);
            switch (entry.Kind)
            {
                case PathComponentKind.Missing: return null; // Nearest existing ancestors passed.
                case PathComponentKind.DriveUnavailable: return "Drive not available; retrying.";
                case PathComponentKind.AccessDenied: return "Access denied while checking the local path.";
                case PathComponentKind.CloudPlaceholder: return "Cloud placeholder, not supported.";
                case PathComponentKind.SymbolicLink: return "Symbolic links are not supported for log paths.";
                case PathComponentKind.Rejected: return "Unsupported reparse point or local storage metadata.";
                case PathComponentKind.MountPoint:
                    var target = entry.Target;
                    if (target is null) return "Unsupported local path.";
                    // NT substitute names are data, never opened before root classification.
                    if (target.StartsWith(@"\??\", StringComparison.Ordinal)) target = target[4..];
                    if (target.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)) target = @"\\?\" + target;
                    var rest = string.Join("\\", components.Skip(i + 1));
                    return Check(target.TrimEnd('\\') + (rest.Length == 0 ? "\\" : "\\" + rest), depth + 1);
            }
        }
        return null;
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
