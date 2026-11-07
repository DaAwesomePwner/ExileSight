using ExileSight.Core.Setup;
using ExileSight.Platform.Abstractions.Install;
using Xunit;

namespace ExileSight.Core.Tests.Setup;

public sealed class PathRejectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void UnavailableOrRemoteDriveIsRejectedBeforeAnyComponentOpen(int type)
    {
        var probe = new Probe(type, PathComponentKind.Ordinary);
        var resolver = new CheckedLocalPathResolver(probe);
        Assert.False(ClientLogDiscovery.IsLocalPath(@"E:\logs\Client.txt", resolver));
        Assert.Contains(type == 4 ? "Network" : "Drive not available",
            ClientLogDiscovery.Validate(@"E:\logs\Client.txt", resolver));
        Assert.Equal(0, probe.Opens);
    }

    [Theory]
    [InlineData(PathComponentKind.AccessDenied, "Access denied")]
    [InlineData(PathComponentKind.Rejected, "Unsupported reparse")]
    [InlineData(PathComponentKind.CloudPlaceholder, "Cloud placeholder, not supported")]
    [InlineData(PathComponentKind.DriveUnavailable, "Drive not available")]
    public void RejectionsExplainTheirActualCause(PathComponentKind kind, string message)
    {
        var resolver = new CheckedLocalPathResolver(new Probe(3, kind));
        var result = ClientLogDiscovery.Validate(@"E:\logs\Client.txt", resolver);
        Assert.Contains(message, result);
        Assert.DoesNotContain("network", result);
    }

    [Theory]
    [InlineData(0x80000013u, 0u, PathComponentKind.Ordinary)]
    [InlineData(0x9000001Au, 0u, PathComponentKind.Ordinary)]
    [InlineData(0x9000F01Au, 0u, PathComponentKind.Ordinary)]
    [InlineData(0x9000001Au, 0x1000u, PathComponentKind.CloudPlaceholder)]
    [InlineData(0x9000001Au, 0x40000u, PathComponentKind.CloudPlaceholder)]
    [InlineData(0x9000101Au, 0x400000u, PathComponentKind.CloudPlaceholder)]
    [InlineData(0x80000099u, 0u, PathComponentKind.Rejected)]
    public void AllowsOnlyKnownLocalStorageTagsAndHydratedCloudFiles(uint tag, uint attributes, PathComponentKind expected)
    {
        Assert.Equal(expected, LocalReparsePolicy.Classify(tag, attributes));
        var resolver = new CheckedLocalPathResolver(new Probe(3, LocalReparsePolicy.Classify(tag, attributes)));
        Assert.Equal(expected == PathComponentKind.Ordinary, ClientLogDiscovery.IsLocalPath(@"E:\Client.txt", resolver));
    }

    private sealed class Probe(int type, PathComponentKind kind) : IPathComponentProbe
    {
        public int Opens;
        public int DriveType(string root) => type;
        public bool IsRemoteDrive(string root) => type == 4;
        public PathComponent Inspect(string path) { Opens++; return new(kind); }
    }
}
