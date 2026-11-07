using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Install;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class DiscoveryTests
{
    [Fact]
    public void SteamFixtureSupportsModernAndLegacyLibraries()
    {
        const string fixture = """
            "libraryfolders" { "0" { "path" "C:\\Steam" "apps" { "238960" "12345" } } "1" "D:\\Games" }
            "TimeNextStatsReport" "1234"
            """;
        Assert.Equal([@"C:\Steam", @"D:\Games"], ClientLogDiscovery.SteamLibraries(fixture));
    }

    [Fact]
    public void RegistryHintsPrecedeSteamThenDefaultsAndDuplicatesAreRemoved()
    {
        var candidates = ClientLogDiscovery.Candidates(new Locations());
        Assert.Equal(Path.Combine("standalone", "logs", "Client.txt"), candidates[0]);
        Assert.Equal(Path.Combine("standalone", "logs", "KakaoClient.txt"), candidates[1]);
        Assert.Equal(Path.Combine("standalone", "logs", "LatestClient.txt"), candidates[2]);
        Assert.Equal(Path.Combine("steam", "steamapps", "common", "Path of Exile", "logs", "Client.txt"), candidates[3]);
        Assert.Equal(Path.Combine("epic", "logs", "LatestClient.txt"), candidates[^1]);
        Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("Client.txt")]
    [InlineData("KakaoClient.txt")]
    [InlineData("LatestClient.txt")]
    public void ValidationReadsWithoutChangingBytes(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, name);
            Assert.NotNull(ClientLogDiscovery.Validate(path));
            File.WriteAllText(path, "synthetic log fixture");
            using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            Assert.Null(ClientLogDiscovery.Validate(path));
            Assert.Equal("synthetic log fixture", File.ReadAllText(path));
            Assert.NotNull(ClientLogDiscovery.Validate(Path.Combine(root, "production_Config.ini")));
            Assert.NotNull(ClientLogDiscovery.Validate(null));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class Locations : IInstallLocations
    {
        public IEnumerable<string> StandaloneLocations() => ["standalone", "STANDALONE"];
        public IEnumerable<string> SteamLocations() => ["steam"];
        public IEnumerable<string> DefaultLocations() => ["standalone", "epic"];
        public string? ReadLibraryFolders(string root) => "\"path\" \"/secondary\"";
    }
}
