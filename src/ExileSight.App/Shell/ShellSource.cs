using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using ExileSight.App.DataPacks;
using ExileSight.Core.Features;
using ExileSight.Core.Settings;
using ExileSight.Core.Setup;
using ExileSight.Core.Status;
using ExileSight.Features.MapTracker;
using ExileSight.Features.Vaalasync;
using ExileSight.Platform.Abstractions.Storage;
using ExileSight.Platform.Abstractions.Window;
using Microsoft.Extensions.DependencyInjection;
namespace ExileSight.App.Shell;

public sealed class ShellSource(IServiceProvider services)
{
    internal T? Model<T>() where T : class => services.GetRequiredService<FeatureHost>().Overlays
        .Select(o => o.ViewModel).OfType<T>().FirstOrDefault();
    public ShellModel Read(string page)
    {
        var settings = services.GetRequiredService<ISettingsStore<SettingsDocument>>().Snapshot;
        var league = settings.Prices.League ?? Model<TrackerModel>()?.League ?? "";
        var game = services.GetRequiredService<IGameWindow>().Current;
        var log = services.GetRequiredService<ClientLogSession>().Snapshot().First(p => p.Key == "Status").Value;
        var packs = services.GetRequiredService<PackStartup>().Status;
        var bindings = FeatureDefaultBinding.Apply(settings.Hotkeys, services.GetRequiredService<FeatureHost>().DefaultBindings);
        var keys = bindings.Select(b => new KeyRow(KeyName(b), TriggerNames.Display(b.Id), b.Enabled)).ToArray();
        var runs = Model<TrackerModel>()?.Query(league) ?? [];
        var sales = Model<ShopModel>()?.History.Snapshot() ?? [];
        var session = LastSession.Map(runs.Select(r => new SessionMap(new DateTimeOffset(r.Started), new DateTimeOffset(r.Ended), r.Seconds,
            r.DeathsKnown ? r.Deaths : null)), sales.Where(s => s.League == league).Select(s =>
            new SessionSale(s.Sold, s.Prices.LastOrDefault(p => s.Sold is not null && p.Time <= s.Sold)?.Note.Amount ?? 0,
                s.Prices.LastOrDefault(p => s.Sold is not null && p.Time <= s.Sold)?.Note.Currency ?? "", s.Purchase)));
        return new(page, league, [HomeStatus.Game(game.Found, game.IsMinimized, game.IsForeground), HomeStatus.Log(log),
            CachedPrices(league, settings.Prices.RefreshInterval), HomeStatus.Packs(packs)], keys, session);
    }
    private static string KeyName(PersistedBinding binding)
    {
        var key = binding.Key == 0x14 ? "CapsLock" : KeyInterop.KeyFromVirtualKey(binding.Key).ToString();
        return binding.Modifiers == 0 ? key : binding.Modifiers.ToString().Replace(", ", " + ") + " + " + key;
    }
    private StatusTile CachedPrices(string league, TimeSpan interval)
    {
        // Read the existing Currency cache only; never start a refresh from the Home page.
        try
        {
            var uri = ExileSight.Core.Pricing.PriceEndpoints.Overview(league, "Currency");
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri.ToUpperInvariant())));
            var path = Path.Combine(services.GetRequiredService<IAppPaths>().RootDirectory, "cache", "poe1", "poeninja", key + ".json");
            if (!File.Exists(path)) return HomeStatus.Prices(false, true, "No saved Currency prices for this league.");
            if (new FileInfo(path).Length > 64 * 1024 * 1024) return HomeStatus.Prices(false, true, "Price cache unavailable.");
            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid cache shape.");
            var available = root.TryGetProperty("Json", out var data) && data.ValueKind == JsonValueKind.String;
            if (available) ExileSight.Core.Pricing.PriceParser.Parse(data.GetString()!, "Currency");
            var updated = root.TryGetProperty("UpdatedAt", out var stamp) && stamp.ValueKind == JsonValueKind.String
                ? stamp.GetDateTimeOffset() : (DateTimeOffset?)null;
            var error = root.TryGetProperty("Error", out var failure) && failure.ValueKind == JsonValueKind.String;
            return HomeStatus.Prices(available, error || updated is null || updated > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - updated >= interval,
                updated is null ? "Saved Currency prices have no timestamp." : "Currency prices saved " + updated.Value.ToLocalTime().ToString("g"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or OverflowException)
        { return HomeStatus.Prices(false, true, "Price cache unavailable."); }
    }
}
