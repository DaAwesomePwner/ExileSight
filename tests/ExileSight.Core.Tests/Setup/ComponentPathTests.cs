using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Install;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class ComponentPathTests
{
    [Theory]
    [InlineData(PathComponentKind.SymbolicLink, @"\\server\share", false)]
    [InlineData(PathComponentKind.MountPoint, @"\??\UNC\server\share", false)]
    [InlineData(PathComponentKind.MountPoint, @"\??\D:\local", true)]
    [InlineData(PathComponentKind.MountPoint, @"\??\Volume{12345678-1234-1234-1234-123456789abc}\", true)]
    public void ClassifiesLinkTextBeforeOpeningTarget(PathComponentKind kind, string target, bool accepted)
    {
        var probe = new Probe(kind, target);
        var resolver = new CheckedLocalPathResolver(probe);
        Assert.Equal(accepted, ClientLogDiscovery.IsLocalPath(@"C:\link\Client.txt", resolver));
        Assert.DoesNotContain(probe.Opened, p => p.Contains("server") || p.Contains(@"link\"));
        if (!accepted) Assert.Equal([@"C:\link"], probe.Opened);
    }

    [Fact]
    public void MissingDescendantsAreLocalWithoutOpeningThem()
    {
        var probe = new Probe(PathComponentKind.Missing, null);
        Assert.True(ClientLogDiscovery.IsLocalPath(@"C:\link\logs\Client.txt", new CheckedLocalPathResolver(probe)));
        Assert.Equal([@"C:\link"], probe.Opened);
    }

    private sealed class Probe(PathComponentKind kind, string? target) : IPathComponentProbe
    {
        public List<string> Opened { get; } = [];
        public bool IsRemoteDrive(string root) => false;
        public PathComponent Inspect(string path)
        {
            Assert.DoesNotContain("server", path);
            Opened.Add(path);
            return path == @"C:\link" ? new(kind, target) : new(PathComponentKind.Ordinary);
        }
    }
}
