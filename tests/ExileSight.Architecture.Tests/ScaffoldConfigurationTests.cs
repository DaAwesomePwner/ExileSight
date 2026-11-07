using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class ScaffoldConfigurationTests
{
    [Fact]
    public void SpikesHaveFinalInteropAndDpiConfiguration()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Root, "src/ExileSight.Platform.Windows/NativeMethods.json")));
        Assert.True(json.RootElement.GetProperty("allowMarshaling").GetBoolean());
        Assert.False(json.RootElement.GetProperty("public").GetBoolean());
        Assert.False(json.RootElement.GetProperty("useSafeHandles").GetBoolean());
        Assert.False(json.RootElement.GetProperty("friendlyOverloads").GetProperty("enabled").GetBoolean());
        var app = XDocument.Load(Path.Combine(Repository.Root, "src/ExileSight.App/ExileSight.App.csproj"));
        Assert.Equal("true", app.Descendants("UseWPF").Single().Value);
        Assert.Equal("app.manifest", app.Descendants("ApplicationManifest").Single().Value);
        var manifest = XDocument.Load(Path.Combine(Repository.Root, "src/ExileSight.App/app.manifest"));
        Assert.Contains(manifest.Descendants(), e => e.Name.LocalName == "dpiAwareness" && e.Value == "PerMonitorV2");
    }

    [Fact]
    public void BuildEnforcesBannedDiagnosticAndNamespaceStyle()
    {
        var props = XDocument.Load(Path.Combine(Repository.Root, "Directory.Build.props"));
        Assert.Equal("true", props.Descendants("EnforceCodeStyleInBuild").Single().Value);
        var targets = XDocument.Load(Path.Combine(Repository.Root, "Directory.Build.targets"));
        Assert.Contains("RS0030", targets.Descendants("WarningsAsErrors").Single().Value.Split(';'));
        Assert.Equal("", targets.Descendants("WarningsNotAsErrors").Single().Value);
    }
}
