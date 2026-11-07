using ExileSight.Core.Logs.Redaction;

namespace ExileSight.RedactLog;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0) return Usage();
        string? keep = null;
        string? output = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--keep" && i + 1 < args.Length) keep = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
            else return Usage();
        }
        try
        {
            // CreateNew also refuses aliases/hardlinks to existing input: never overwrite an existing file.
            using var reader = new StreamReader(new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            await using var file = output is null ? null : new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            await using var writer = file is null ? null : new StreamWriter(file);
            var redactor = new LogRedactor(keep);
            while (await reader.ReadLineAsync() is { } line)
                await (writer ?? Console.Out).WriteLineAsync(redactor.Redact(line));
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"Redaction failed: {e.Message}");
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: dotnet run --project tools/ExileSight.RedactLog -- EXCERPT [--keep NAME] [--output NEW_FILE]");
        return 2;
    }
}
