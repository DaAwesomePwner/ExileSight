using System.Text.Json;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Features;

/// <summary>Compiled-in modules only. Registration completes before input starts.</summary>
public sealed class FeatureHost
{
    private readonly Func<string, IntentToken?, GameAction, ActionResult> execute;
    private readonly List<IFeatureCommandHandler> commands = [];
    private readonly List<IOmniHandler> omni = [];
    private readonly List<(string FeatureId, Action<LogEvent> Handler)> subscriptions = [];
    private readonly List<(string FeatureId, object ViewModel)> overlays = [];
    public FeatureHost(IEnumerable<IFeature> features,
        Func<string, string, JsonElement?> readSetting, Action<string, string, JsonElement> writeSetting,
        Func<string, IntentToken?, GameAction, ActionResult>? execute = null)
    {
        this.execute = execute ?? ((_, _, _) => new(ActionOutcome.Failed, "No gate configured"));
        var modules = features.ToArray();
        Manifests = Array.AsReadOnly(modules.Select(f => f.Manifest).ToArray());
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in Manifests)
        {
            manifest.Validate();
            if (!ids.Add(manifest.Id)) throw new ArgumentException("Duplicate feature ID");
        }
        foreach (var feature in modules)
        {
            var context = new Context(this, feature.Manifest, readSetting, writeSetting);
            feature.Register(context);
            context.Close();
        }
    }
    public IReadOnlyList<FeatureManifest> Manifests { get; }
    public IReadOnlyList<IFeatureCommandHandler> Commands => commands.AsReadOnly();
    public IReadOnlyList<(string FeatureId, object ViewModel)> Overlays => overlays.AsReadOnly();
    public bool HasLogSubscriptions => subscriptions.Count != 0;
    public void Publish(LogEvent value, Action<string, Exception>? report = null)
    {
        foreach (var (featureId, handler) in subscriptions)
        {
            try { handler(value); }
            catch (Exception error) { report?.Invoke(featureId, error); }
        }
    }
    public bool Route(ParsedItem item, OmniContext context, OmniPressKind press)
    {
        var handler = omni.OrderByDescending(h => h.Priority).FirstOrDefault(h => h.Matches(item, context));
        if (handler is null) return false;
        handler.Handle(item, context, press);
        return true;
    }
    private sealed class RegisteredCommand(string owner, string name, ActionKind? action,
        Func<IntentToken?, ActionResult> execute) : IFeatureCommandHandler
    {
        public string FeatureId => owner;
        public string Command => name;
        public ActionKind? RequiredAction => action;
        public ActionResult Execute() => Execute(null);
        public ActionResult Execute(IntentToken? token) => action is not null && token is null
            ? new(ActionOutcome.MissingToken) : execute(token);
    }
    private sealed class Context(FeatureHost host, FeatureManifest manifest,
        Func<string, string, JsonElement?> read, Action<string, string, JsonElement> write) : IFeatureContext
    {
        private bool closed;
        internal void Close() => closed = true;
        private void Registering() { if (closed) throw new InvalidOperationException("Registration has completed"); }
        public ActionResult Execute(IntentToken? token, GameAction action) => host.execute(manifest.Id, token, action);
        public void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute)
        {
            Registering();
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(execute);
            if (action is { } kind && !manifest.GameActions.Contains(kind)) throw new ArgumentException("Undeclared action");
            var qualified = manifest.Id + ":" + command;
            if (host.commands.Any(c => c.Command == qualified)) throw new ArgumentException("Duplicate command");
            host.commands.Add(new RegisteredCommand(manifest.Id, qualified, action, execute));
        }
        public void RegisterOmniHandler(IOmniHandler handler) { Registering(); host.omni.Add(handler); }
        public void Subscribe<T>(Action<T> handler) where T : LogEvent
        { Registering(); host.subscriptions.Add((manifest.Id, value => { if (value is T typed) handler(typed); })); }
        public void RegisterOverlay(object viewModel) { Registering(); host.overlays.Add((manifest.Id, viewModel)); }
        public JsonElement? ReadSetting(string key) => read(manifest.Id, key)?.Clone();
        public void WriteSetting(string key, JsonElement value) => write(manifest.Id, key, value.Clone());
    }
}
