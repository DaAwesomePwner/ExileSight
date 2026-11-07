using System.Text.Json;
using ExileSight.Core.DataPacks;
using ExileSight.DataGen;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class ConfigurationTests
{
    private const string PinnedSha = "d0bc438357d849bb93ad22f3db0b12938aaf6da1";

    [Theory]
    [InlineData(null)]
    [InlineData("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")]
    public async Task CliDefaultsToApprovedPinAndExplicitShaWins(string? sha)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var expected = sha?.ToLowerInvariant() ?? PinnedSha;
        try
        {
            var args = new List<string> { "poe1", "--out", directory, "--source-dir", Path.Combine(AppContext.BaseDirectory, "fixtures") };
            if (sha is not null) args.AddRange(["--repoe-sha", sha]);
            Assert.Equal(0, await Program.Main(args.ToArray()));
            var manifest = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(directory, "poe1-bases.manifest.json")), PackJson.Default.PackManifest)!;
            Assert.Equal(expected, manifest.Source.Sha);
            Assert.Equal(expected, manifest.PackVersion);
            Assert.Contains("RePoE fork at " + expected, manifest.Attribution);
            Assert.Contains("© Grinding Gear Games", manifest.Attribution);
            Assert.Contains("owner's approval per D-047", manifest.Attribution);
            Assert.DoesNotContain("until redistribution is cleared", manifest.Attribution);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task InvalidOverrideNeverFallsBackToPin() =>
        Assert.Equal(1, await Program.Main(["poe1", "--out", "unused", "--repoe-sha", "master"]));
}
