namespace ExileSight.Platform.Abstractions.Install;

public enum PathComponentKind { Ordinary, Missing, MountPoint, SymbolicLink, Rejected, DriveUnavailable, AccessDenied, CloudPlaceholder }
public sealed record PathComponent(PathComponentKind Kind, string? Target = null);

/// <summary>Inspect the named entry without following its reparse point. Ancestors are already checked.</summary>
public interface IPathComponentProbe
{
    int DriveType(string root) => IsRemoteDrive(root) ? 4 : 3;
    bool IsRemoteDrive(string root);
    PathComponent Inspect(string path);
}
