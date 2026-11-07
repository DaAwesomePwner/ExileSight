namespace ExileSight.Platform.Abstractions.Updates;

public abstract record UpdateStatus
{
    public sealed record UpToDate : UpdateStatus;
    public sealed record Available(string Version, string? Notes = null, bool Downloaded = false) : UpdateStatus;
    public sealed record Unavailable(string Reason) : UpdateStatus;
}

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default);
    Task<UpdateStatus> DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    /// <summary>Arm a downloaded update for normal exit; never exits or restarts the app.</summary>
    void ApplyOnRestart();
}
