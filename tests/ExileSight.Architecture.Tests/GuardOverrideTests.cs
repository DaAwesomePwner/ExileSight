using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class GuardOverrideTests
{
    [Theory]
    [InlineData("<ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>")]
    [InlineData("<ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>")]
    [InlineData("<DirectoryBuildTargetsPath>empty.targets</DirectoryBuildTargetsPath>")]
    [InlineData("<DirectoryBuildPropsPath>empty.props</DirectoryBuildPropsPath>")]
    [InlineData("<_SkipAnalyzers>true</_SkipAnalyzers>")]
    [InlineData("<CompilerResponseFile>hidden.txt</CompilerResponseFile>")]
    [InlineData("/skipanalyzers+")]
    [InlineData("/nowarn:RS0030")]
    [InlineData("<Target Name=\"ExileSightGuardBeforeCompile\" />")]
    [InlineData("<Target Name='ExileSightGuardAfterCompile' />")]
    public void DetectsBuildControlOverrides(string text) => Assert.True(BoundaryScanner.GuardOverride(text));

    [Fact]
    public async Task TimeoutTerminatesAndReapsOwnedChild()
    {
        var directory = Path.Combine(Repository.Root, "tests/ExileSight.Architecture.Tests/obj", "timeout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var project = Path.Combine(directory, "Timeout.proj");
            await File.WriteAllTextAsync(project, """
                <Project>
                  <UsingTask TaskName="Delay" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
                    <Task><Code Type="Fragment" Language="cs">System.Threading.Thread.Sleep(30000);</Code></Task>
                  </UsingTask>
                  <Target Name="WaitForTimeout"><Delay /></Target>
                </Project>
                """);
            var error = await Assert.ThrowsAsync<TimeoutException>(() => DotnetProcess.Run(directory,
                ["msbuild", project, "-t:WaitForTimeout", "-nr:false"], TimeSpan.FromSeconds(1)));
            Assert.Contains("terminated and reaped", error.Message);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
