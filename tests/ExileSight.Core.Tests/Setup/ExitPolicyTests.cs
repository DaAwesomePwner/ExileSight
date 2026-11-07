using ExileSight.Core.Setup;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class ExitPolicyTests
{
    [Fact]
    public void SessionEndSuppressesApplyAndShortensStopButNextNormalExitApplies()
    {
        var policy = new ExitPolicy();
        Assert.True(policy.ApplyUpdateOnExit);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.StopTimeout);
        policy.BeginSessionEnding();
        policy.BeginSessionEnding();
        Assert.False(policy.ApplyUpdateOnExit);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.StopTimeout);
        Assert.True(new ExitPolicy().ApplyUpdateOnExit);
    }
}
