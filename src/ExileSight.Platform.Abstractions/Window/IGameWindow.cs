namespace ExileSight.Platform.Abstractions.Window;

/// <summary>Start and dispose on the message-loop thread; changes are delivered on that thread.</summary>
public interface IGameWindow : IDisposable
{
    GameWindowState Current { get; }
    string? LastError { get; }
    event EventHandler? Changed;
    void Start();
}
