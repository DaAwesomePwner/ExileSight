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
    private readonly List<FeatureDefaultBinding> defaults = [];
    private readonly List<(string Owner, IOmniHandler Handler)> omni = [];
    private readonly Action<Action> post;
    private readonly List<(string FeatureId, Action<LogEvent> Handler)> subscriptions = [];
    private readonly List<(string FeatureId, object ViewModel)> overlays = [];
    public FeatureHost(IEnumerable<IFeature> features,
        Func<string, string, JsonElement?> readSetting, Action<string, string, JsonElement> writeSetting,
        Func<string, IntentToken?, GameAction, ActionResult>? execute = null, Action<Action>? dispatch = null)
    {
        post = dispatch ?? new FeatureWorkQueue().Post;
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
    public IReadOnlyList<FeatureDefaultBinding> DefaultBindings => defaults.AsReadOnly();
    public bool HasLogSubscriptions => subscriptions.Count != 0;
    public void Publish(LogEvent value, Action<string, Exception>? report = null)
    {
        foreach (var (featureId, handler) in subscriptions)
        {
            try { handler(value); }
            catch (Exception error) { report?.Invoke(featureId, error); }
        }
    }
    public bool Route(ParsedItem item, OmniContext context, OmniPressKind press) =>
        new OmniRouter(omni.Select(o => o.Handler)).Route(item, context, press);
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
    private sealed class PressCommand(string owner, string name, ActionKind action,
        Func<IntentToken?, PressStarted, ActionResult> press, Action<PressReleased> release) : IPressCommandHandler
    {
        public string FeatureId => owner;
        public string Command => name;
        public ActionKind? RequiredAction => action;
        public ActionResult Execute() => new(ActionOutcome.MissingToken);
        public ActionResult Press(IntentToken? token, PressStarted started) => token is null
            ? new(ActionOutcome.MissingToken) : press(token, started);
        public void Release(PressReleased released) => release(released);
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
        public void RegisterPressCommand(string command, ActionKind action,
            Func<IntentToken?, PressStarted, ActionResult> press, Action<PressReleased> release)
        {
            Registering();
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(press);
            ArgumentNullException.ThrowIfNull(release);
            if (!manifest.GameActions.Contains(action)) throw new ArgumentException("Undeclared action");
            var qualified = manifest.Id + ":" + command;
            if (host.commands.Any(c => c.Command == qualified)) throw new ArgumentException("Duplicate command");
            host.commands.Add(new PressCommand(manifest.Id, qualified, action, press, release));
        }
        public void RegisterDefaultBinding(FeatureDefaultBinding binding)
        {
            Registering();
            if (!host.commands.Any(c => c.FeatureId == manifest.Id && c.Command == binding.Command))
                throw new ArgumentException("Default binding must target an owned command");
            if (host.defaults.Any(b => b.Id.Equals(binding.Id, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Duplicate default binding");
            host.defaults.Add(binding);
        }
        public IReadOnlyList<IOmniHandler> OmniHandlers =>
            host.omni.Where(o => o.Owner == manifest.Id).Select(o => o.Handler).ToArray();
        public void DispatchOmni(ParsedItem item, OmniContext context, OmniPressKind press) =>
            host.post(() => host.Route(item, context, press));
        public void RegisterOmniHandler(IOmniHandler handler) { Registering(); host.omni.Add((manifest.Id, handler)); }
        public void Subscribe<T>(Action<T> handler) where T : LogEvent
        { Registering(); host.subscriptions.Add((manifest.Id, value => { if (value is T typed) handler(typed); })); }
        public void RegisterOverlay(object viewModel) { Registering(); host.overlays.Add((manifest.Id, viewModel)); }
        public JsonElement? ReadSetting(string key) => read(manifest.Id, key)?.Clone();
        public void WriteSetting(string key, JsonElement value) => write(manifest.Id, key, value.Clone());
    }
}
