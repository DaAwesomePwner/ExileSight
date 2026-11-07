using Xunit;

namespace ExileSight.BanCoverage.Tests;

public sealed class FixtureTests
{
    [Fact]
    public void InventoryIsTestOnly() => Assert.EndsWith(".Tests", typeof(FixtureTests).Assembly.GetName().Name);
}
