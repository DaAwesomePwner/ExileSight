namespace ExileSight.DataGen;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length < 3 || args[0] != "poe1" || args.Length % 2 != 1)
                throw new ArgumentException("Usage: datagen poe1 --out <dir> [--repoe-sha <sha>] [--source-dir <export>]");
            var options = new Dictionary<string, string>();
            for (var i = 1; i < args.Length; i += 2)
            {
                if (args[i] is not ("--repoe-sha" or "--out" or "--source-dir")) throw new ArgumentException("Unknown option.");
                options.Add(args[i], args[i + 1]);
            }
            var sha = options.GetValueOrDefault("--repoe-sha", DataGenConfiguration.SourceSha).ToLowerInvariant();
            RepoeConverter.ValidateSha(sha);
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ExileSight/1.0 (+https://github.com/DaAwesomePwner/ExileSight)");
            async Task<byte[]> Read(string file)
            {
                if (options.TryGetValue("--source-dir", out var source)) return await File.ReadAllBytesAsync(Path.Combine(source, file));
                // Constructed exclusively from a validated SHA and fixed filenames: no arbitrary hosts or branches.
                return await client.GetByteArrayAsync($"https://raw.githubusercontent.com/{RepoeConverter.Repository}/{sha}/data/{file}");
            }
            var packs = RepoeConverter.Convert(await Read("base_items.json"), await Read("mods.json"), sha);
            await PackWriter.WriteAsync(options["--out"], packs);
            Console.WriteLine(RepoeConverter.Attribution(sha));
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or IOException or HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            Console.Error.WriteLine("DataGen input or download failed: " + ex.Message);
            return 1;
        }
    }
}
