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
        Assert.Equal(Path.Combine(Path.GetTempPath(), "standalone", "logs", "Client.txt"), candidates[0]);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "standalone", "logs", "KakaoClient.txt"), candidates[1]);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "standalone", "logs", "LatestClient.txt"), candidates[2]);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "steam", "steamapps", "common", "Path of Exile", "logs", "Client.txt"), candidates[3]);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "epic", "logs", "LatestClient.txt"), candidates[^1]);
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

    [Theory]
    [InlineData(@"\\server\logs\Client.txt")]
    [InlineData("//server/logs/Client.txt")]
    [InlineData(@"\\?\UNC\server\logs\Client.txt")]
    [InlineData("logs/Client.txt")]
    [InlineData(@"C:logs\Client.txt")]
    public void RejectsNetworkAndRelativePathsBeforeOpening(string path)
    {
        Assert.False(ClientLogDiscovery.IsLocalAbsolutePath(path));
        Assert.Contains("local path", ClientLogDiscovery.Validate(path));
    }

    [Fact]
    public void VdfNetworkLibrariesAreIgnored()
    {
        Assert.Empty(ClientLogDiscovery.SteamLibraries("\"path\" \"//server/steam\""));
        Assert.Empty(ClientLogDiscovery.SteamLibraries("\"path\" \"relative\""));
    }

    private sealed class Locations : IInstallLocations
    {
        public IEnumerable<string> StandaloneLocations() => [Path.Combine(Path.GetTempPath(), "standalone"), Path.Combine(Path.GetTempPath(), "STANDALONE"), @"\\server\game"];
        public IEnumerable<string> SteamLocations() => [Path.Combine(Path.GetTempPath(), "steam"), "//server/steam"];
        public IEnumerable<string> DefaultLocations() => [Path.Combine(Path.GetTempPath(), "standalone"), Path.Combine(Path.GetTempPath(), "epic")];
        public string? ReadLibraryFolders(string root)
        {
            Assert.False(root.StartsWith("//"));
            return "\"path\" \"C:/secondary\"";
        }
    }
}
