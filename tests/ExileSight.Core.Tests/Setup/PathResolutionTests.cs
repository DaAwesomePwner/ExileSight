using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Install;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class PathResolutionTests
{
    [Theory]
    [InlineData(true, @"C:\logs\Client.txt", false)]
    [InlineData(false, @"\\?\UNC\server\share\Client.txt", false)]
    [InlineData(false, @"\\server\share\Client.txt", false)]
    [InlineData(false, @"\\?\C:\logs\Client.txt", true)]
    [InlineData(false, null, false)]
    public void ChecksDriveAndResolvedTarget(bool remote, string? final, bool expected)
    {
        var resolver = new Resolver(remote, final);
        Assert.Equal(expected, ClientLogDiscovery.IsLocalPath(@"C:\logs\Client.txt", resolver));
        if (!expected) Assert.Contains("local path", ClientLogDiscovery.Validate(@"C:\logs\Client.txt", resolver));
    }

    [Fact]
    public void DiscoveryRejectsRemoteRootsBeforeReadingSteamMetadata()
    {
        Assert.Empty(ClientLogDiscovery.Candidates(new Locations(), new Resolver(true, null)));
    }

    private sealed class Resolver(bool remote, string? final) : ILocalPathResolver
    {
        public bool IsRemoteDrive(string path) => remote;
        public string? FinalPath(string path) => final;
    }
    private sealed class Locations : IInstallLocations
    {
        public IEnumerable<string> StandaloneLocations() => [@"C:\Game"];
        public IEnumerable<string> SteamLocations() => [@"C:\Steam"];
        public IEnumerable<string> DefaultLocations() => [@"C:\Default"];
        public string? ReadLibraryFolders(string steamRoot) => throw new InvalidOperationException("Must not read remote metadata");
    }
}
