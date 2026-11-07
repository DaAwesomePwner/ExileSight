using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

public interface ITransactionalSettingsStore<T> : ISettingsStore<T>
{
    /// <summary>Publish changes only after persistence succeeds; failures do not schedule retries.</summary>
    void Commit(string section, Action<T> update);
}
