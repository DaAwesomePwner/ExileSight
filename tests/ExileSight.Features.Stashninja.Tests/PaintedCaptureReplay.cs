namespace ExileSight.Features.Stashninja.Tests;

internal static class PaintedCaptureReplay
{
    // Fresh non-P identifications switch earlier; replay Unknown to prove the slower hide bound.
    public static bool Hides(GridIdentification stale, Layout page) => RecoveryTicks(stale, page) == 2;

    public static int RecoveryTicks(GridIdentification stale, Layout page)
    {
        var policy = new StashWatchPolicy();
        var before = new double[PageSignature.Length];
        var after = Enumerable.Repeat(.2, PageSignature.Length).ToArray();
        var completed = DateTimeOffset.UnixEpoch;
        policy.Accept(before, before);
        if (stale.Layout == page) policy.ObserveIdentification(completed, after, after);
        else policy.Retain(completed, true, after, after, adoptBaseline: false);
        if (policy.Tick(completed.AddMilliseconds(450), after, after).Work != WatchWork.Identify) return -1;
        if (policy.Retain(completed.AddMilliseconds(550), false, after, after)) return -1;
        if (policy.Tick(completed.AddMilliseconds(900), after, after).Work != WatchWork.Identify) return -1;
        return policy.Retain(completed.AddMilliseconds(1000), false, after, after) && !policy.Identified ? 2 : -1;
    }
}
