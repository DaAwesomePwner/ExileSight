using System.IO;
using ExileSight.Core.Pricing;
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

public sealed class ShellSource(IServiceProvider services) : IDisposable
{
    private PriceCacheSnapshotReader? priceCache;
    private (string Path, DateTime Stamp, long Size)? runSignature;
    private MapRun[] savedRuns = [];
    public Task<ShellModel> ReadAsync(string page) => Task.Run(() => Read(page));
    internal T? Model<T>() where T : class => services.GetRequiredService<FeatureHost>().Overlays
        .Select(o => o.ViewModel).OfType<T>().FirstOrDefault();
    private readonly Pages.ItemRulesTranslations translations = new(() => services.GetRequiredService<PackStartup>().Packs
        .TryGet<ExileSight.Core.DataPacks.PackDocument>("poe1/stats")?.CreateCopy().Translations ?? [], services.GetRequiredService<PackStartup>().Packs);
    internal Pages.ItemRulesTranslations RuleTranslations => translations;
    public void Dispose() => translations.Dispose();
    private ShellModel Read(string page)
    {
        var settings = services.GetRequiredService<ISettingsStore<SettingsDocument>>().Snapshot;
        var league = settings.Prices.League ?? Model<TrackerModel>()?.League ?? "";
        var game = services.GetRequiredService<IGameWindow>().Current;
        var log = services.GetRequiredService<ClientLogSession>().Snapshot().First(p => p.Key == "Status").Value;
        var packs = services.GetRequiredService<PackStartup>().Status;
        var bindings = FeatureDefaultBinding.Apply(settings.Hotkeys, services.GetRequiredService<FeatureHost>().DefaultBindings);
        var keys = bindings.Select(b => new KeyRow(KeyName(b), TriggerNames.Display(b.Id), b.Enabled)).ToArray();
        var runs = ReadRuns(league);
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
    private MapRun[] ReadRuns(string league)
    {
        var paths = services.GetRequiredService<IAppPaths>();
        var path = new RunStore(Path.Combine(paths.GetGameDirectory("poe1"), "maptracker")).PathFor(league);
        var file = new FileInfo(path);
        var signature = (path, file.Exists ? file.LastWriteTimeUtc : default, file.Exists ? file.Length : -1);
        if (signature != runSignature)
        {
            savedRuns = Model<TrackerModel>()?.Query(league) ?? [];
            runSignature = signature;
        }
        return savedRuns;
    }
    private StatusTile CachedPrices(string league, TimeSpan interval)
    {
        priceCache ??= new(services.GetRequiredService<IAppPaths>().RootDirectory);
        var cached = priceCache.Read(league, "Currency");
        var now = DateTimeOffset.UtcNow;
        return HomeStatus.Prices(cached.Available,
            cached.Diagnostic is not null || cached.UpdatedAt is null || cached.UpdatedAt > now || now - cached.UpdatedAt >= interval,
            cached.Diagnostic ?? (cached.UpdatedAt is { } updated
                ? "Currency prices saved " + StatusFormat.Date(updated.ToLocalTime())
                : "Saved Currency prices have no timestamp."));
    }
}
