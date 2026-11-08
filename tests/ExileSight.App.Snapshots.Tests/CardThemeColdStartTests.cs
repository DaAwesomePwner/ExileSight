using Xunit;
#if SNAPSHOT_WINDOWS
using System.Runtime.ExceptionServices;
using System.Windows;
using ExileSight.App.Ui;
using ExileSight.Core.Settings;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class CardThemeColdStartTests
{
    // Run this test alone in a fresh testhost to exercise the cold WPF resource path:
    // dotnet test --filter FullyQualifiedName~CardThemeColdStartTests
    [WindowsOnlyFact]
    public void DefaultLoadsBeforeAnyOtherWpfUse()
    {
#if SNAPSHOT_WINDOWS
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Do not read Application.Current or construct a visual before this call:
                // either can initialize WPF's application resource infrastructure.
                var theme = CardTheme.Default;
                Assert.True(theme.OverlayForeground.IsFrozen);
                Assert.Equal((byte)0xE8, theme.OverlayForeground.Color.R);
                Assert.Same(theme, CardTheme.Default);
                Assert.NotNull(CardTheme.For(OverlayDensity.Compact, OverlayContrast.HighContrast));
                Assert.Null(Application.Current);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
#endif
    }
}
