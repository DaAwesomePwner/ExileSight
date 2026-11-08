using ExileSight.Core.Status;
using ExileSight.Features.Guide;
namespace ExileSight.App.Shell;

public sealed record KeyRow(string Keys, string Name, bool Enabled);
public sealed record ShellModel(string Page, string League, IReadOnlyList<StatusTile> Status,
    IReadOnlyList<KeyRow> Keys, LastSession? Session)
{
    public GuideSnapshot? Guide { get; init; }
    public bool SameContent(ShellModel other) => Page == other.Page && League == other.League &&
        GuidePageState.SameContent(Guide, other.Guide) && Status.SequenceEqual(other.Status) && Keys.SequenceEqual(other.Keys) && SameSession(Session, other.Session);
    private static bool SameSession(LastSession? a, LastSession? b) => a is null ? b is null : b is not null &&
        a.Started == b.Started && a.Ended == b.Ended && a.AverageSeconds == b.AverageSeconds &&
        a.KnownDeaths == b.KnownDeaths && a.DeathsKnown == b.DeathsKnown && a.DivineSold == b.DivineSold && a.Maps.SequenceEqual(b.Maps);
}
