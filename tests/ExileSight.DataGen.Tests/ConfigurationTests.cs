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
            Assert.Contains("RePoE fork project (commit " + expected + ")", manifest.Attribution);
            Assert.Contains("© Grinding Gear Games", manifest.Attribution);
            Assert.Contains("permission of the project owner", manifest.Attribution);
            Assert.Contains("not affiliated with or endorsed by Grinding Gear Games", manifest.Attribution);
            Assert.DoesNotContain("D-047", manifest.Attribution);
            Assert.DoesNotContain("until redistribution is cleared", manifest.Attribution);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitShaDoesNotReadConfiguration(bool invalidOverride)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var result = await Program.RunAsync(["poe1", "--out", directory,
                "--source-dir", Path.Combine(AppContext.BaseDirectory, "fixtures"),
                "--repoe-sha", invalidOverride ? "master" : PinnedSha],
                () => throw new InvalidOperationException("Configuration must not be read."));
            Assert.Equal(invalidOverride ? 1 : 0, result);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("missing-sha")]
    [InlineData("invalid-sha")]
    [InlineData("wrong-kind")]
    [InlineData("unreadable")]
    public async Task ConfigurationErrorsReturnFailure(string error)
    {
        string ReadBrokenConfiguration() => throw (error switch
        {
            "missing" => new InvalidOperationException("Missing embedded RePoE configuration."),
            "malformed" => new JsonException("Invalid configuration JSON."),
            "missing-sha" => new KeyNotFoundException("Missing sourceSha."),
            "invalid-sha" => new ArgumentException("Invalid sourceSha."),
            "wrong-kind" => new InvalidOperationException("sourceSha must be a string."),
            _ => (Exception)new IOException("Unable to read configuration.")
        });
        Assert.Equal(1, await Program.RunAsync(["poe1", "--out", "unused"], ReadBrokenConfiguration));
    }

    [Fact]
    public async Task InvalidOverrideNeverFallsBackToPin() =>
        Assert.Equal(1, await Program.Main(["poe1", "--out", "unused", "--repoe-sha", "master"]));
}
