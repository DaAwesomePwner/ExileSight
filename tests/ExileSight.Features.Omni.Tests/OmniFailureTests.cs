using ExileSight.Core.Features;
using ExileSight.Features.Omni;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Shell;
using Xunit;

namespace ExileSight.Features.Omni.Tests;

public sealed class OmniFailureTests
{
    private const string Item = "Item Class: Rings\nRarity: Normal\nGold Ring";
    [Fact]
    public void FailedCopyDoesNotOpenMenuEvenWithText()
    {
        var feature = new OmniFeature();
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Failed, CopiedText: Item));
        var opened = 0;
        feature.Menu.Opened += _ => opened++;
        var command = (IPressCommandHandler)host.Commands.Single();
        command.Press(new(), new(1, 100));
        command.Release(new(1, 100, 500));
        Assert.Equal(0, opened);
    }
    [Fact]
    public void ANewPressReplacesAnItemWhoseReleaseWasLost()
    {
        var feature = new OmniFeature();
        var copies = 0;
        var host = new FeatureHost([feature], (_, _) => null, (_, _, _) => { },
            (_, _, _) => new(ActionOutcome.Success, CopiedText: ++copies == 1 ? Item : null));
        var opened = 0;
        feature.Menu.Opened += _ => opened++;
        var command = (IPressCommandHandler)host.Commands.Single();
        command.Press(new(), new(1, 100));
        command.Press(new(), new(2, 2100));
        command.Release(new(1, 100, 2200));
        command.Release(new(2, 2100, 2400));
        Assert.Equal(2, copies);
        Assert.Equal(0, opened);
    }
    [Theory]
    [InlineData("https://evil.www.poewiki.net/wiki/Test")]
    [InlineData("http://www.poewiki.net/wiki/Test")]
    [InlineData("https://www.poewiki.net.evil.org/")]
    public void BrowserAllowlistIsExact(string url) =>
        Assert.Throws<ArgumentException>(() => OmniMenu.Open(new ValidatingBrowser(), new("invalid", url)));
    private sealed class ValidatingBrowser : IBrowser
    {
        public void Open(string url, IReadOnlySet<string> hosts) => BrowserUrl.Validate(url, hosts);
    }
}
