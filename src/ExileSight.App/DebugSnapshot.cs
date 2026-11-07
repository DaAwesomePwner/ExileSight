using ExileSight.Core.Diagnostics;

namespace ExileSight.App;

internal sealed record DebugSnapshot(string Title, IReadOnlyList<KeyValuePair<string, string>> Rows)
{
    internal static DebugSnapshot Capture(IDebugSection section)
    {
        var title = "Diagnostics";
        try
        {
            title = section.Title;
            return new(title, section.Snapshot().ToArray());
        }
        catch (Exception error)
        {
            return new(title, [new("error", error.Message)]);
        }
    }
}
