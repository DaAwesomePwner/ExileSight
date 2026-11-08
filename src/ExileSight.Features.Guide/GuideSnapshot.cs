using ExileSight.Core.DataPacks;

namespace ExileSight.Features.Guide;

/// <summary>Steps and progress are scoped to applicable pages in the current act.</summary>
public sealed record GuideSnapshot(bool Visible, int? Act, int Step, int StepCount,
    TimeSpan ActTime, TimeSpan TotalTime, double Progress, IReadOnlyList<string> Lines,
    string? NextLine, string? Notice, bool Complete)
{
    internal static GuideSnapshot Create(bool visible, GuideProgress? progress, GuideData? data,
        GuideData? displayData, bool league, string bandit, string? storeNotice, string? notice)
    {
        if (progress is null || data is null || displayData is null)
            return new(visible, null, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, [], null,
                "Waiting for poe1/guide pack.", false);
        var pages = data.Pages.Where(p => p.Condition is null || p.Condition.Kind switch
        {
            "league-start" => p.Condition.Values.Contains(league ? "yes" : "no"),
            "bandit" => p.Condition.Values.Contains(bandit), _ => false
        }).ToArray();
        var act = progress.Current.Act;
        var count = pages.Count(p => p.Act == act);
        var step = pages.Take(progress.Index + 1).Count(p => p.Act == act);
        // Store failures take precedence, followed by uncertainty and completion.
        notice = storeNotice ?? (progress.Uncertain ? "Timing uncertain: log gap/source change." :
            progress.Complete ? "Guide complete" : notice);
        return new(visible, act, step, count, TimeSpan.FromMilliseconds(progress.ActMilliseconds[act]),
            TimeSpan.FromMilliseconds(progress.ActMilliseconds.Sum()), (double)step / count,
            Array.AsReadOnly(progress.Current.Lines.Select(l => GuideDisplay.Line(l.Text, displayData)).ToArray()),
            !progress.Complete && progress.Next?.Lines.FirstOrDefault() is { } next
                ? GuideDisplay.Line(next.Text, displayData) : null, notice, progress.Complete);
    }
}
