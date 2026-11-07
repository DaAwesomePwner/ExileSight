namespace ExileSight.Platform.Abstractions.Storage;

public interface IAppPaths
{
    string RootDirectory { get; }
    string LogsDirectory { get; }
    string GetGameDirectory(string game);
    string GetSettingsDirectory(string game);
}

public sealed class SettingsChangedEventArgs(string section) : EventArgs
{
    public string Section { get; } = section;
}

/// <summary>Snapshots are detached; updates are serialized. Flush and Dispose surface save failures.</summary>
public interface ISettingsStore<T> : IDisposable
{
    T Snapshot { get; }
    bool IsReadOnly { get; }
    IReadOnlyList<string> Warnings { get; }
    event EventHandler<SettingsChangedEventArgs>? Changed;
    void Update(string section, Action<T> update);
    void Flush();
}

public sealed record ProfileInfo(string Id, string Name);

public interface IProfileStore : IDisposable
{
    IReadOnlyList<ProfileInfo> Profiles { get; }
    string ActiveProfileId { get; }
    string Create(string name);
    void Rename(string id, string name);
    void Delete(string id);
    void Switch(string id);
}
