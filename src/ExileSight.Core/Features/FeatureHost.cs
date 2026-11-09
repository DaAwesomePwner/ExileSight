using System.Text.Json;
using ExileSight.Core.Items;
using ExileSight.Core.Logs;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Features;

/// <summary>Compiled-in modules only. Registration completes before input starts.</summary>
public sealed partial class FeatureHost
{
    private readonly Func<string, IntentToken?, GameAction, ActionResult> execute;
    private readonly List<IFeatureCommandHandler> commands = [];
    private readonly List<IItemMenuProvider> itemMenus = [];
    private readonly List<IItemCardComparisonProvider> cardComparisons = [];
    private readonly List<FeatureDefaultBinding> defaults = [];
    private readonly List<(string Owner, IOmniHandler Handler)> omni = [];
    private readonly Action<Action> post;
    private readonly IPackReader packs;
    private readonly List<(string FeatureId, Action<LogEvent> Handler)> subscriptions = [];
    private readonly List<(string FeatureId, object ViewModel)> overlays = [];
    public FeatureHost(IEnumerable<IFeature> features,
        Func<string, string, JsonElement?> readSetting, Action<string, string, JsonElement> writeSetting,
        Func<string, IntentToken?, GameAction, ActionResult>? execute = null, Action<Action>? dispatch = null, IPackReader? packs = null)
    {
        this.packs = packs ?? EmptyPackReader.Instance;
        post = dispatch ?? new FeatureWorkQueue { ReportFailure = type => ReportWorkFailure(type) }.Post;
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
    // App composition sets this before input starts; portable hosts may leave diagnostics disabled.
    public Action<string> ReportWorkFailure { get; set; } = _ => { };
    public IReadOnlyList<FeatureManifest> Manifests { get; }
    public IReadOnlyList<IFeatureCommandHandler> Commands => commands.AsReadOnly();
    public IReadOnlyList<(string FeatureId, object ViewModel)> Overlays => overlays.AsReadOnly();
    public IReadOnlyList<FeatureDefaultBinding> DefaultBindings => defaults.AsReadOnly();
    public bool HasLogSubscriptions => subscriptions.Count != 0 || logRegistrations.Count != 0;
    // Synchronous injection retained for portable feature contract tests; production uses ConnectLogs.
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
        var copy = CopiedItem.From(item);
        foreach (var observer in copiedObservers)
            try { observer.Handler(copy); } catch (Exception error) { ReportWorkFailure(observer.Owner + ":" + error.GetType().Name); }
        return new OmniRouter(omni.Select(o => o.Handler)).Route(item, context, press);
    }
    private sealed class RegisteredCommand(string owner, string name, ActionKind? action,
        Func<IntentToken?, ActionResult> execute, CommandKind kind) : IFeatureCommandHandler, IFeatureCommandPolicy
    {
        public string FeatureId => owner;
        public CommandKind Kind => kind;
        public string Command => name;
        public ActionKind? RequiredAction => action;
        public ActionResult Execute() => Execute(null);
        public ActionResult Execute(IntentToken? token) => action is not null && token is null
            ? new(ActionOutcome.MissingToken) : execute(token);
    }
    private sealed class PressCommand(string owner, string name, ActionKind? action,
        Func<IntentToken?, PressStarted, ActionResult> press, Action<PressReleased> release) : IPressCommandHandler
    {
        public string FeatureId => owner;
        public string Command => name;
        public ActionKind? RequiredAction => action;
        public ActionResult Execute() => new(ActionOutcome.MissingToken);
        public ActionResult Press(IntentToken? token, PressStarted started) => action is not null && token is null
            ? new(ActionOutcome.MissingToken) : press(action is null ? null : token, started);
        public void Release(PressReleased released) => release(released);
    }
    private sealed class Context(FeatureHost host, FeatureManifest manifest,
        Func<string, string, JsonElement?> read, Action<string, string, JsonElement> write) : IFeatureContext
    {
        public void ObserveCopiedItems(Action<CopiedItem> observer)
        { Registering(); ArgumentNullException.ThrowIfNull(observer); host.copiedObservers.Add((manifest.Id, observer)); }
        public IPackReader Packs { get; } = new FeaturePacks(host, manifest);
        private bool closed;
        internal void Close() => closed = true;
        private void Registering() { if (closed) throw new InvalidOperationException("Registration has completed"); }
        public ActionResult Execute(IntentToken? token, GameAction action) => host.execute(manifest.Id, token, action);
        public void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute)
            => RegisterCommand(command, action, execute, CommandKind.Bindable);
        public void RegisterCommand(string command, ActionKind? action, Func<IntentToken?, ActionResult> execute,
            CommandKind kind)
        {
            Registering();
            if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(execute);
            if (action is { } actionKind && !manifest.GameActions.Contains(actionKind)) throw new ArgumentException("Undeclared action");
            var qualified = manifest.Id + ":" + command;
            if (host.commands.Any(c => c.Command == qualified)) throw new ArgumentException("Duplicate command");
            host.commands.Add(new RegisteredCommand(manifest.Id, qualified, action, execute, kind));
        }
        public void RegisterPressCommand(string command, ActionKind? action,
            Func<IntentToken?, PressStarted, ActionResult> press, Action<PressReleased> release)
        {
            Registering();
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(press);
            ArgumentNullException.ThrowIfNull(release);
            if (action is { } actionKind && !manifest.GameActions.Contains(actionKind)) throw new ArgumentException("Undeclared action");
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
        public void DispatchFeature(Action work) => host.post(work);
        public void DispatchOmni(ParsedItem item, OmniContext context, OmniPressKind press) =>
            host.post(() => host.Route(item, context, press));
        public void RegisterOmniHandler(IOmniHandler handler) { Registering(); host.omni.Add((manifest.Id, handler)); }
        public void Subscribe<T>(Action<T> handler) where T : LogEvent
        {
            Registering();
            Action<LogEvent> dispatch = value => { if (value is T typed) handler(typed); };
            host.subscriptions.Add((manifest.Id, dispatch));
            host.logRegistrations.Add(new(manifest.Id, e => dispatch(e.Event), null));
        }
        public void SubscribeLog(Action<ClientLogEvent> handler, LogReplay? replay = null)
        {
            Registering(); ArgumentNullException.ThrowIfNull(handler);
            if (replay is not null) ArgumentOutOfRangeException.ThrowIfLessThan(replay.MaxEvents, 1);
            host.logRegistrations.Add(new(manifest.Id, handler, replay));
        }
        public IReadOnlyList<IItemCardComparisonProvider> ItemCardComparisons => host.cardComparisons.AsReadOnly();
        public void RegisterItemCardComparison(IItemCardComparisonProvider provider)
        { Registering(); ArgumentNullException.ThrowIfNull(provider); host.cardComparisons.Add(provider); }
        public IReadOnlyList<IItemMenuProvider> ItemMenus => host.itemMenus.AsReadOnly();
        public void RegisterItemMenu(IItemMenuProvider provider)
        { Registering(); ArgumentNullException.ThrowIfNull(provider); host.itemMenus.Add(new OwnedItemMenuProvider(manifest.Id, provider)); }
        public void RegisterOverlay(object viewModel) { Registering(); host.overlays.Add((manifest.Id, viewModel)); }
        public JsonElement? ReadSetting(string key) => read(manifest.Id, key)?.Clone();
        public void WriteSetting(string key, JsonElement value) => write(manifest.Id, key, value.Clone());
    }
}
