using ExileSight.Core.Diagnostics;
using Xunit;

namespace ExileSight.Core.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void DebugContractBelongsToCore() =>
        Assert.Equal("ExileSight.Core", typeof(IDebugSection).Assembly.GetName().Name);
}
