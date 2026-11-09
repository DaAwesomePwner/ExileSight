using System.Text.RegularExpressions;
using Xunit;
namespace ExileSight.Architecture.Tests;

public sealed class ScreenSamplerConsumerTests
{
    [Fact]
    public void OnlyStashViewsAndRegistrationConsumeScreenSampler()
    {
        foreach (var path in Repository.Files("*.cs").Where(Repository.IsProduction))
        {
            var code = BoundaryScanner.Code(File.ReadAllText(path));
            if (!Regex.IsMatch(code, @"\bI?ScreenSampler\b")) continue;
            var relative = Repository.Relative(path);
            // The implementation declares the concrete type; all consumers below
            // may name it only in the DI registration.
            if (relative == "src/ExileSight.Platform.Windows/Window/ScreenSampler.cs") continue;
            Assert.True(AllowedConsumer(relative, code), $"Forbidden screen sampler dependency in {relative}");
        }
    }
    private static bool AllowedConsumer(string path, string code)
    {
        if (path == "src/ExileSight.App/Overlay/OverlayRegistration.cs") return true;
        if (Regex.IsMatch(code, @"\bScreenSampler\b")) return false;
        if (path == "src/ExileSight.Platform.Abstractions/Window/IScreenSampler.cs") return true;
        return path.StartsWith("src/ExileSight.App/Features/Stashninja/", StringComparison.Ordinal) &&
            Regex.IsMatch(code, @"namespace\s+ExileSight\.App\.Features\.Stashninja\s*;");
    }
    [Theory]
    [InlineData("IScreenSampler sampler;", true)]
    [InlineData("ScreenSampler sampler;", false)]
    [InlineData("var sampler = new ScreenSampler(game);", false)]
    [InlineData("using Sampler = ExileSight.Platform.Windows.Window.ScreenSampler;", false)]
    public void StashMayOnlyUseTheInterface(string code, bool allowed)
    {
        code = "namespace ExileSight.App.Features.Stashninja; " + code;
        Assert.Equal(allowed, AllowedConsumer("src/ExileSight.App/Features/Stashninja/Probe.cs", code));
        Assert.True(AllowedConsumer("src/ExileSight.App/Overlay/OverlayRegistration.cs", code));
        Assert.False(AllowedConsumer("src/ExileSight.App/Features/Other/Probe.cs", code));
    }
}
