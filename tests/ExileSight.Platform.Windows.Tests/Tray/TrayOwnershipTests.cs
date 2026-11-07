using ExileSight.Platform.Windows.Tray;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Tray;

public sealed class TrayOwnershipTests
{
    [WindowsOnlyFact]
    public void RejectsNullAndInvalidCallbackWindowsBeforeRegisteringIcon()
    {
        var factory = new ShellTrayIconFactory();
        Assert.Throws<ArgumentException>(() => factory.Create(0, 0));
        Assert.Throws<ArgumentException>(() => factory.Create(-123, 0));
        Assert.Throws<ArgumentException>(() => factory.Create(-3, -3)); // HWND_MESSAGE is a parent sentinel, not an owned HWND.
    }
}
