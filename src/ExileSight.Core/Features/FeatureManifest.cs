using System.Collections.Frozen;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Input;

namespace ExileSight.Core.Features;

public sealed class FeatureManifest
{
    public FeatureManifest(string id, IEnumerable<Game> games, IEnumerable<ActionKind> gameActions,
        IEnumerable<string> dataPacks, IEnumerable<string> networkHosts, string description,
        IEnumerable<string>? services = null, IEnumerable<string>? browserHosts = null,
        IEnumerable<string>? screenReads = null)
    {
        Id = id; Games = games.ToFrozenSet(); GameActions = gameActions.ToFrozenSet();
        DataPacks = dataPacks.ToFrozenSet(StringComparer.Ordinal);
        NetworkHosts = networkHosts.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Services = (services ?? []).ToFrozenSet(StringComparer.Ordinal); Description = description;
        BrowserHosts = (browserHosts ?? []).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        ScreenReads = (screenReads ?? []).ToFrozenSet(StringComparer.Ordinal);
        Validate();
    }
    public FrozenSet<string> ScreenReads { get; }
    public FrozenSet<string> BrowserHosts { get; }
    public string Id { get; }
    public FrozenSet<Game> Games { get; }
    public FrozenSet<ActionKind> GameActions { get; }
    public FrozenSet<string> DataPacks { get; }
    public FrozenSet<string> NetworkHosts { get; }
    public FrozenSet<string> Services { get; }
    public string Description { get; }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.Equals(Id, "builtin", StringComparison.OrdinalIgnoreCase) || !Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            throw new ArgumentException("Feature ID must be a non-reserved identifier");
        if (Games.Count == 0 || Games.Any(g => !Enum.IsDefined(g)) || GameActions.Any(a => !Enum.IsDefined(a)))
            throw new ArgumentException("Unknown game or action");
        if (string.IsNullOrWhiteSpace(Description) || DataPacks.Any(string.IsNullOrWhiteSpace) || Services.Any(string.IsNullOrWhiteSpace) ||
            ScreenReads.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Description and resource identifiers must be nonempty");
        foreach (var host in BrowserHosts)
        {
            var uri = ExileSight.Platform.Abstractions.Shell.BrowserUrl.Validate("https://" + host + "/", [host]);
            if (uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("Invalid browser host");
        }
        if (NetworkHosts.Any(h => h is not ("poe.ninja" or "github.com" or "api.github.com" or "raw.githubusercontent.com" or "objects.githubusercontent.com")))
            throw new ArgumentException("Network host is not approved; browser links are separate from HTTP access");
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DeclaredActionsAttribute(params ActionKind[] actions) : Attribute
{
    public IReadOnlyList<ActionKind> Actions { get; } = Array.AsReadOnly(actions);
}
