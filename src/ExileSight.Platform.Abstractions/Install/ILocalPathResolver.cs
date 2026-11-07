namespace ExileSight.Platform.Abstractions.Install;

/// <summary>Read-only component checks without following unclassified links. Missing local descendants
/// return their local path; null means remote, unsafe, or inaccessible.</summary>
public interface ILocalPathResolver
{
    string? Rejection(string path)
    {
        const string rejected = "Choose a fully-qualified local path; network paths are not allowed.";
        if (IsRemoteDrive(path)) return rejected;
        var final = FinalPath(path);
        if (final is null) return rejected;
        var normalized = final.Replace('\\', '/');
        if (normalized.StartsWith("//?/UNC/", StringComparison.OrdinalIgnoreCase)) return rejected;
        if (normalized.StartsWith("//?/", StringComparison.Ordinal)) normalized = normalized[4..];
        if (normalized.StartsWith("//", StringComparison.Ordinal)) return rejected;
        return IsRemoteDrive(final) ? rejected : null;
    }
    bool IsRemoteDrive(string path);
    string? FinalPath(string path);
}
