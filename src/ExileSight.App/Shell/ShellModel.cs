using ExileSight.Core.Status;
namespace ExileSight.App.Shell;

public sealed record KeyRow(string Keys, string Name, bool Enabled);
public sealed record ShellModel(string Page, string League, IReadOnlyList<StatusTile> Status,
    IReadOnlyList<KeyRow> Keys, LastSession? Session);
