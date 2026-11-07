using ExileSight.Core.DataPacks;

namespace ExileSight.Core.Features;

/// <summary>Passive pack access. Null means missing or incompatible data; no IO occurs on reads.</summary>
public interface IPackReader
{
    PackSnapshot<T>? TryGet<T>(string packId) where T : class;
    /// <summary>Raised on the publisher's thread, after publication. Marshal UI work as needed.
    /// Subscribe before reading initial state. Notifications carry no input authority.</summary>
    event Action<PackChange>? Changed;
}

public sealed record PackChange(string PackId, string? Version, string Source)
{
    public bool Available => Version is not null;
}

public sealed class EmptyPackReader : IPackReader
{
    public static IPackReader Instance { get; } = new EmptyPackReader();
    private EmptyPackReader() { }
    public PackSnapshot<T>? TryGet<T>(string packId) where T : class => null;
    public event Action<PackChange>? Changed { add { } remove { } }
}
