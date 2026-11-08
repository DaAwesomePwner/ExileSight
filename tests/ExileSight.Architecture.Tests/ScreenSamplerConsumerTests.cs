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
            if (relative is "src/ExileSight.Platform.Abstractions/Window/IScreenSampler.cs" or
                "src/ExileSight.Platform.Windows/Window/ScreenSampler.cs" or
                "src/ExileSight.App/Overlay/OverlayRegistration.cs") continue;
            Assert.StartsWith("src/ExileSight.App/Features/Stashninja/", relative);
            Assert.Matches(@"namespace\s+ExileSight\.App\.Features\.Stashninja\s*;", code);
        }
    }
}
