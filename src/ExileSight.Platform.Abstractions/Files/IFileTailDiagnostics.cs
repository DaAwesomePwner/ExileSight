namespace ExileSight.Platform.Abstractions.Files;

public enum FileTailCondition { Unavailable, AccessDenied, OversizedLine }
public sealed record FileTailDiagnostic(FileTailCondition Condition, string Message);

/// <summary>Passive, privacy-safe diagnostics. Subscribers must not throw or synthesize input.</summary>
public interface IFileTailDiagnostics
{
    event Action<FileTailDiagnostic>? Diagnostic;
}
