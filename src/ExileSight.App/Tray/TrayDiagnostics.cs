using ExileSight.Core.Diagnostics;

namespace ExileSight.App.Tray;

public sealed class TrayDiagnostics : IDebugSection
{
    private string lastError = "None";
    public string Title => "Application / tray";
    public void Record(Exception error) => Volatile.Write(ref lastError, error.GetType().Name + ": " + error.Message);
    public IReadOnlyList<KeyValuePair<string, string>> Snapshot() => [new("Last error", Volatile.Read(ref lastError))];
}
