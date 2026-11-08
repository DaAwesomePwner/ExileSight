namespace ExileSight.Features.Guide;

/// <summary>Desktop page state retained by its shell, independently of rebuilt controls.</summary>
public sealed class GuidePageState
{
    public string? Status { get; set; }

    // Compare only page content: the desktop page does not display timers or overlay visibility.
    public static bool SameContent(GuideSnapshot? left, GuideSnapshot? right) =>
        left is null ? right is null : right is not null &&
        left.Act == right.Act && left.Step == right.Step && left.StepCount == right.StepCount &&
        left.LeagueStart == right.LeagueStart && left.Bandit == right.Bandit &&
        left.HasCustomGuide == right.HasCustomGuide && left.Notice == right.Notice &&
        left.Complete == right.Complete && left.NextLine == right.NextLine && left.Lines.SequenceEqual(right.Lines);
}
