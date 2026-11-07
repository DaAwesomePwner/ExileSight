using System.Diagnostics;
using System.Text.Json;
using ExileSight.Core.Games;
using ExileSight.Core.Settings;
using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Perf.Tests;

public sealed class PublishedAppBudgetTests(ITestOutputHelper output)
{
    [PublishedAppFact]
    public async Task PublishedTrayAppMeetsIdleBudget()
    {
        var executable = Path.GetFullPath(Environment.GetEnvironmentVariable("EXILESIGHT_PERF_APP")!);
        Assert.True(File.Exists(executable), $"Published app missing: {executable}");
        // Use a disposable Windows account. Never overwrite a developer's settings/cache.
        var paths = new AppPaths();
        Assert.False(Directory.Exists(paths.RootDirectory),
            "Run under a disposable Windows account with no existing ExileSight data.");
        var reportPath = Path.GetFullPath(Environment.GetEnvironmentVariable("EXILESIGHT_PERF_REPORT") ?? "perf-report.json");
        var samples = new List<Sample>();
        Process? process = null;
        double? cpuPercent = null;
        long peakPrivateBytes = 0;
        string? failure = null;
        var requestReport = Environment.GetEnvironmentVariable("EXILESIGHT_PERF_REQUEST_REPORT") == "1";
        try
        {
            Directory.CreateDirectory(paths.RootDirectory);
            var log = Path.Combine(paths.RootDirectory, "Client.txt");
            await File.WriteAllTextAsync(log, "");
            using (var profiles = new ProfileStore(paths, Game.PoE1))
            using (var settings = profiles.Open(profiles.ActiveProfileId))
            {
                settings.Update("general", value =>
                {
                    value.General.FirstRunCompleted = true;
                    value.General.ClientTxtPath = log;
                });
                settings.Flush();
            }
            process = Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false,
                Environment = { ["EXILESIGHT_PERF_MEMORY_REPORT"] = "after-measurement" }
            }) ?? throw new InvalidOperationException("Could not launch published app.");
            Assert.True(process.WaitForInputIdle(30_000), "App did not create its UI message loop.");
            // Allow JIT, initial network refresh and pack preparation to settle.
            await Task.Delay(TimeSpan.FromSeconds(60));
            EnsureTrayOnly(process);
            process.Refresh();
            var initialCpu = process.TotalProcessorTime;
            var clock = Stopwatch.StartNew();
            do
            {
                EnsureTrayOnly(process);
                process.Refresh();
                var elapsed = clock.Elapsed.TotalSeconds;
                var cpu = (process.TotalProcessorTime - initialCpu).TotalSeconds;
                var memory = process.PrivateMemorySize64;
                peakPrivateBytes = Math.Max(peakPrivateBytes, memory);
                samples.Add(new(elapsed, cpu, memory));
                if (elapsed >= 60) break;
                await Task.Delay(TimeSpan.FromSeconds(1));
            } while (true);
            var last = samples[^1];
            cpuPercent = 100 * last.CpuSeconds / last.Seconds;
            // CPU/private sampling is finished before any own-process diagnostic reads,
            // JSON serialization or diagnostic file writes are requested.
            if (requestReport)
            {
                await File.WriteAllTextAsync(Path.Combine(paths.RootDirectory, "perf-memory.request"), "snapshot");
                var deadline = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(paths.RootDirectory, "perf-memory.json")) && deadline.Elapsed < TimeSpan.FromSeconds(10))
                    await Task.Delay(100);
                Assert.True(File.Exists(Path.Combine(paths.RootDirectory, "perf-memory.json")), "Requested own-process report missing.");
            }
            Assert.True(cpuPercent < 1, $"Idle CPU {cpuPercent:F3}% >= 1% of one core.");
            Assert.True(peakPrivateBytes < 200_000_000, $"Private bytes {peakPrivateBytes} >= 200 MB.");
        }
        catch (Exception error) { failure = error.Message; throw; }
        finally
        {
            // Test-only child-process control; production never opens foreign process handles.
            if (process is not null)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                process.Dispose();
            }
            var memoryPath = Path.Combine(paths.RootDirectory, "perf-memory.json");
            JsonElement? ownProcessMemory = File.Exists(memoryPath)
                ? JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(memoryPath)) : null;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
            {
                executable, revision = Environment.GetEnvironmentVariable("PERF_REVISION"),
                os = Environment.OSVersion.ToString(), logicalProcessors = Environment.ProcessorCount,
                settleSeconds = 60, cpuPercent, peakPrivateBytes,
                cpuMarginPercentagePoints = 1 - cpuPercent,
                memoryMarginBytes = 200_000_000 - peakPrivateBytes,
                failure, diagnosticsDuringMeasurement = false, reportRequestedAfterMeasurement = requestReport, ownProcessMemory, samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            await DeleteTestDataAsync(paths.RootDirectory);
        }
    }

    private async Task DeleteTestDataAsync(string path)
    {
        const int attempts = 5;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException error)
            {
                if (attempt == attempts)
                {
                    // Preserve the budget result even if a transient lock outlives our retries.
                    output.WriteLine($"Cleanup warning after {attempts} attempts: {error.Message}");
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }
    }

    private static void EnsureTrayOnly(Process process)
    {
        process.Refresh();
        Assert.False(process.HasExited, "App exited before completing the measurement.");
        Assert.Equal(IntPtr.Zero, process.MainWindowHandle); // Wizard/startup-error dialogs fail the test.
    }

    private sealed record Sample(double Seconds, double CpuSeconds, long PrivateBytes);
}
