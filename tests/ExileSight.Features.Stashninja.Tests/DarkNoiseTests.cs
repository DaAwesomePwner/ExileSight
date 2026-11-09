using Xunit;
using Xunit.Abstractions;

namespace ExileSight.Features.Stashninja.Tests;

public sealed class DarkNoiseTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorrelatedDarkNoiseAndFlatFramesNeverAdmitOrIdentify(bool clipBeforeBlur)
    {
        var layouts = Layouts.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "layouts"));
        var failures = new List<string>(); double maxScore = double.NegativeInfinity, maxPre = 0;
        foreach (var width in new[] { 2000, 3440 })
        {
            var size = new Resolution(width, width == 2000 ? 837 : 1440);
            void Check(byte[] pixels, string name)
            {
                var pre = GridPrecheck.Score(layouts, size, _ => null, a => GridFixture.Crop(pixels, size, a));
                var result = GridIdentifier.Identify(layouts, size, new(0, 0, size.Width, size.Height, pixels), _ => null, pre, buttons: GridFixture.Buttons);
                maxScore = Math.Max(maxScore, result.Score); maxPre = Math.Max(maxPre, pre.Values.Max());
                if (result.Layout is not null || pre.Values.Any(StashWatchPolicy.ShouldIdentify))
                    failures.Add($"{width}/{name}: {result.Layout?.TabId}/{result.Layout?.Page}, score {result.Score:F2}, pre {pre.Values.Max():F2}");
            }
            foreach (var grey in new byte[] { 0, 128 })
            {
                var pixels = new byte[size.Width * size.Height * 4]; Array.Fill(pixels, grey);
                Check(pixels, $"flat-{grey}");
            }
            foreach (var mean in new[] { 5, 15 })
            foreach (var sigma in new[] { 30, 60 })
            foreach (var blur in new[] { 3, 7 })
            foreach (var seed in new[] { 0, 1, 2, 3 })
                Check(Render(size, mean, sigma, blur, seed, clipBeforeBlur), $"mean{mean}-sigma{sigma}-blur{blur}-seed{seed}");
        }
        output.WriteLine($"clipBeforeBlur={clipBeforeBlur}: 64 correlated scenes + 4 flat frames: max score {maxScore:F3}, max precheck {maxPre:F3}");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static byte[] Render(Resolution size, double mean, double sigma, int blur, int seed, bool clipBeforeBlur)
    {
        var random = new Random(seed); var raw = new float[size.Width * size.Height];
        for (var i = 0; i < raw.Length; i++)
            raw[i] = (float)(mean + sigma * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble()));
        if (clipBeforeBlur)
            for (var i = 0; i < raw.Length; i++) raw[i] = Math.Clamp(raw[i], 0, 255);
        var horizontal = new float[raw.Length]; var radius = blur / 2;
        for (var y = 0; y < size.Height; y++)
        for (var x = 0; x < size.Width; x++)
        {
            double sum = 0;
            for (var d = -radius; d <= radius; d++) sum += raw[y * size.Width + Math.Clamp(x + d, 0, size.Width - 1)];
            horizontal[y * size.Width + x] = (float)(sum / blur);
        }
        var pixels = new byte[raw.Length * 4];
        for (var y = 0; y < size.Height; y++)
        for (var x = 0; x < size.Width; x++)
        {
            double sum = 0;
            for (var d = -radius; d <= radius; d++) sum += horizontal[Math.Clamp(y + d, 0, size.Height - 1) * size.Width + x];
            var i = (y * size.Width + x) * 4; var v = (byte)Math.Clamp(sum / blur, 0, 255);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = v; pixels[i + 3] = 255;
        }
        return pixels;
    }
}
