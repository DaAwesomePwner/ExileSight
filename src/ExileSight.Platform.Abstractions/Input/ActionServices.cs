namespace ExileSight.Platform.Abstractions.Input;

public readonly record struct KeyStroke(ushort Key, bool Down);
public sealed record KeySendResult(bool Success, uint Inserted, string Reason = "");
public interface IKeySender
{
    bool IsGameForeground { get; }
    bool IsStreaming => false;
    bool CanExecuteOnCurrentThread => SynchronizationContext.Current is null;
    KeyModifiers Modifiers { get; }
    // The adapter must release all batch keys immediately after any partial insertion.
    KeySendResult Send(IReadOnlyList<KeyStroke> sequence);
}
public interface IActionAudit
{
    string Path { get; }
    void EnsureWritable();
    void Append(string line);
}
