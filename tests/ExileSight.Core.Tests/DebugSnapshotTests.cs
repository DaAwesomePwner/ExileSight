using ExileSight.App;
using ExileSight.Core.Diagnostics;
using Xunit;

namespace ExileSight.Core.Tests;

public sealed class DebugSnapshotTests
{
    [Fact]
    public void FailingSectionBecomesAnErrorRow()
    {
        var snapshot = DebugSnapshot.Capture(new FailingSection());
        Assert.Equal("Broken", snapshot.Title);
        Assert.Equal(new KeyValuePair<string, string>("error", "failed"), Assert.Single(snapshot.Rows));
    }

    private sealed class FailingSection : IDebugSection
    {
        public string Title => "Broken";
        public IReadOnlyList<KeyValuePair<string, string>> Snapshot() => throw new InvalidOperationException("failed");
    }
}
