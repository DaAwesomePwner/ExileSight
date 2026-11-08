using System.ComponentModel;
using ExileSight.App.Features;
using ExileSight.Core.Features;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Features.ItemInfo.Tests;

public sealed class TooltipFailureTests
{
    [Theory]
    [InlineData(0)] // GetCursorPos access denied
    [InlineData(1)] // Initial native position
    [InlineData(2)] // Position after Show, or DPI refresh of a visible window
    public void NativeFailureClosesAndReleasesLease(int failure)
    {
        var visible = failure == 2;
        var lease = failure == 2;
        var errors = new List<Exception>();
        void Native(int stage) { if (stage == failure) throw new Win32Exception(5); }
        OverlayFailureGuard.Run(() =>
        {
            Native(0);
            Native(1);
            visible = true;
            Native(2);
            lease = true;
        }, () => { lease = false; visible = false; }, errors.Add);
        Assert.False(visible);
        Assert.False(lease);
        Assert.IsType<Win32Exception>(Assert.Single(errors));
    }
    [Fact]
    public void ThrowingComparisonDoesNotDiscardCardOrHealthyComparison()
    {
        var errors = new List<Exception>();
        var feature = new ItemInfoFeature { SectionError = errors.Add };
        var healthy = new Provider(false);
        var host = new FeatureHost([feature, new Provider(true), healthy], (_, _) => null, (_, _, _) => { });
        Assert.True(host.Route(CalculatorTests.Hybrid(), new(Core.Games.Game.PoE1), OmniPressKind.Short));
        Assert.Single(errors);
        Assert.NotEmpty(feature.ViewModel.Card!.Rows);
        Assert.Single(feature.ViewModel.Card.Compare!.Slots);
        Assert.Equal(1, healthy.Calls);
    }
    [Fact]
    public void PackStatusFailureDoesNotDiscardCard()
    {
        var errors = new List<Exception>();
        var feature = new ItemInfoFeature
        { PackStatus = () => throw new InvalidOperationException(), SectionError = errors.Add };
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { });
        Assert.True(host.Route(CalculatorTests.Hybrid(), new(Core.Games.Game.PoE1), OmniPressKind.Short));
        Assert.Single(errors);
        Assert.NotEmpty(feature.ViewModel.Card!.Rows);
        Assert.Null(feature.ViewModel.Notice);
    }

    private sealed class Provider(bool fail) : IFeature, IItemCardComparisonProvider
    {
        public int Calls { get; private set; }
        public FeatureManifest Manifest { get; } = new(fail ? "broken" : "healthy", [Core.Games.Game.PoE1], [], [], [], "test");
        public void Register(IFeatureContext context) => context.RegisterItemCardComparison(this);
        public ItemCardComparison BuildComparison(ParsedItem item)
        {
            Calls++;
            if (fail) throw new InvalidOperationException();
            return new([new("healthy", true, [])], "test");
        }
    }
}
