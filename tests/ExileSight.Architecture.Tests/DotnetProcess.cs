using System.Diagnostics;

namespace ExileSight.Architecture.Tests;

internal static class DotnetProcess
{
    internal static async Task<(int ExitCode, string Output)> Run(string directory, IEnumerable<string> arguments, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.Exited += (_, _) => completion.TrySetResult(process.ExitCode);
        if (!process.Start())
            throw new InvalidOperationException("Could not start dotnet.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            var code = await completion.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(45));
            return (code, await output.WaitAsync(TimeSpan.FromSeconds(5)) + await errors.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        catch (TimeoutException)
        {
            try
            {
#pragma warning disable RS0030 // Test-only process owner: terminate only the child tree started above.
                process.Kill(entireProcessTree: true);
#pragma warning restore RS0030
            }
            catch (InvalidOperationException) { } // Exit raced the timeout.
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(output, errors).WaitAsync(TimeSpan.FromSeconds(10));
            throw new TimeoutException("dotnet exceeded its deadline; its owned process tree was terminated and reaped.");
        }
    }
}
