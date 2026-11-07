using ExileSight.Core.DataPacks;

namespace ExileSight.Core.Features;

public sealed partial class FeatureHost
{
    private sealed class FeaturePacks : IPackReader
    {
        private readonly FeatureHost host;
        private readonly FeatureManifest manifest;
        public FeaturePacks(FeatureHost host, FeatureManifest manifest)
        {
            this.host = host;
            this.manifest = manifest;
            host.packs.Changed += OnChanged;
        }
        public PackSnapshot<T>? TryGet<T>(string packId) where T : class
        {
            if (manifest.DataPacks.Contains(packId)) return host.packs.TryGet<T>(packId);
            Report("UndeclaredPack");
            return null;
        }
        public event Action<PackChange>? Changed;
        private void OnChanged(PackChange change)
        {
            if (!manifest.DataPacks.Contains(change.PackId)) return;
            var handlers = Changed;
            if (handlers is null) return;
            host.post(() =>
            {
                foreach (var handler in handlers.GetInvocationList())
                {
                    try { ((Action<PackChange>)handler)(change); }
                    catch (Exception error) { Report(error.GetType().Name); }
                }
            });
        }
        private void Report(string type)
        {
            try { host.ReportWorkFailure($"{manifest.Id}: {type}"); }
            catch (Exception) { /* Diagnostics cannot prevent subsequent notifications. */ }
        }
    }
}
