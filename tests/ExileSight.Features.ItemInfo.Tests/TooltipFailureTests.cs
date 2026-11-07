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
    public void ThrowingSectionDoesNotDiscardOtherSectionsOrRecalculateItem()
    {
        var analysis = new object();
        var errors = new List<Exception>();
        var healthy = new Provider(analysis, false);
        var sections = TooltipSections.Build(new TooltipContext(new ParsedItem(), analysis),
            [new Provider(analysis, true), healthy], errors.Add);
        Assert.Single(errors);
        Assert.Single(sections);
        Assert.Equal(1, healthy.Calls);
    }
    private sealed class Provider(object expected, bool fail) : ITooltipSectionProvider
    {
        public int Calls { get; private set; }
        public int Priority => 100;
        public TooltipSection? Build(ParsedItem item) => throw new InvalidOperationException("Analysis was lost");
        public TooltipSection Build(TooltipContext context)
        {
            Calls++;
            Assert.Same(expected, context.Analysis);
            if (fail) throw new InvalidOperationException();
            return new("healthy", "Healthy", []);
        }
    }
}
