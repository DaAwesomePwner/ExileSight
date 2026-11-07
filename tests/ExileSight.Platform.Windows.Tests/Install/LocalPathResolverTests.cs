using ExileSight.Core.Setup;
using ExileSight.Platform.Windows.Install;
using Xunit;

namespace ExileSight.Platform.Windows.Tests.Install;

public sealed class LocalPathResolverTests
{
    [WindowsOnlyFact]
    public void NativeComponentProbeAcceptsMissingThenExistingLocalLog()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExileSight-path-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "Client.txt");
            var resolver = new LocalPathResolver();
            Assert.True(ClientLogDiscovery.IsLocalPath(path, resolver));
            Assert.Contains("does not exist", ClientLogDiscovery.Validate(path, resolver));
            File.WriteAllText(path, "synthetic");
            Assert.Null(ClientLogDiscovery.Validate(path, resolver));
        }
        finally { Directory.Delete(root, true); }
    }
}
