namespace ExileSight.Features.Stashninja.Tests;

internal static class PaintedCaptureReplay
{
    // Fresh non-P identifications switch earlier; replay Unknown to prove the slower hide bound.
    public static bool Hides(GridIdentification stale, Layout page)
    {
        var policy = new StashWatchPolicy();
        var before = new double[PageSignature.Length];
        var after = Enumerable.Repeat(.2, PageSignature.Length).ToArray();
        var completed = DateTimeOffset.UnixEpoch;
        policy.Accept(before, before);
        if (stale.Layout == page) policy.ObserveIdentification(completed, after, after);
        else policy.Retain(completed, true, after, after, adoptBaseline: false);
        if (policy.Tick(completed.AddMilliseconds(450), after, after).Work != WatchWork.Identify) return false;
        if (policy.Retain(completed.AddMilliseconds(550), false, after, after)) return false;
        if (policy.Tick(completed.AddMilliseconds(900), after, after).Work != WatchWork.Identify) return false;
        return policy.Retain(completed.AddMilliseconds(1000), false, after, after) && !policy.Identified;
    }
}
