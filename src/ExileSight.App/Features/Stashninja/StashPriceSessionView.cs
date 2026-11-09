using ExileSight.Features.Stashninja;

namespace ExileSight.App.Features.Stashninja;

public sealed partial class StashView
{
    private readonly StashPriceSession priceSession = new();
    private bool WatchEnabled => priceSession.ShouldWatch(model.AutomaticPrices);

    private void TogglePrices()
    {
        priceSession.Toggle(model.AutomaticPrices);
        ResetPriceWatch();
        UpdatePolling();
    }

    private void EndPriceVisit()
    {
        priceSession.EndVisit();
        ResetPriceWatch();
        UpdatePolling();
    }

    private void ResetPriceWatch()
    {
        automaticEpoch++; generation++; autoEpoch++;
        watchPolicy = new(); calibrationConfirmation.Reset();
        automaticPage = null; recognitionHidden = true;
        Invalidate(); surface.Hide();
    }
}
