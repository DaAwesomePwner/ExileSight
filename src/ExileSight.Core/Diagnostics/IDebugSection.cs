namespace ExileSight.Core.Diagnostics;

/// <summary>A diagnostic section polled on the UI thread approximately every 500 ms.</summary>
public interface IDebugSection
{
    string Title { get; }

    /// <summary>Returns a thread-safe, stable snapshot; implementations synchronize with their writers.</summary>
    IReadOnlyList<KeyValuePair<string, string>> Snapshot();
}
