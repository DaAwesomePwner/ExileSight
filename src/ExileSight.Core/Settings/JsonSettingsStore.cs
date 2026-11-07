using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

/// <summary>One owner per file. Thread-safe within that owner. Events run on the updating caller.
/// Dispose flushes pending changes. Timer failures remain dirty and are exposed through Warnings;
/// explicit Flush retries and throws on failure. Newer schemas are refused without deserializing.</summary>
public sealed class JsonSettingsStore<T> : ISettingsStore<T> where T : VersionedDocument
{
    private readonly object sync = new();
    private readonly RootedFiles files;
    private readonly string path;
    private readonly JsonTypeInfo<T> typeInfo;
    private readonly MigrationPipeline migrations;
    private readonly TimeSpan debounce;
    private readonly Timer timer;
    private readonly List<string> warnings = [];
    private T document;
    private bool dirty;
    private bool disposed;

    public JsonSettingsStore(IAppPaths paths, string path, JsonTypeInfo<T> typeInfo, Func<T> defaults,
        MigrationPipeline? migrations = null, TimeSpan? debounce = null)
    {
        files = new(paths.RootDirectory);
        this.path = files.Check(path);
        this.typeInfo = typeInfo;
        this.migrations = migrations ?? new();
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(250);
        if (this.debounce < TimeSpan.Zero || this.debounce.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(debounce));
        document = Load(defaults);
        timer = new Timer(_ => SaveFromTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool IsReadOnly { get; private set; }
    public IReadOnlyList<string> Warnings { get { lock (sync) return warnings.ToArray(); } }
    public T Snapshot { get { lock (sync) { ThrowIfDisposed(); return Clone(document); } } }
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Update(string section, Action<T> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(update);
        lock (sync)
        {
            ThrowIfDisposed();
            if (IsReadOnly) throw new InvalidOperationException("Settings use a newer schema and are read-only.");
            var candidate = Clone(document);
            update(candidate);
            candidate.SchemaVersion = migrations.CurrentVersion;
            SettingsValidation.Validate(candidate);
            document = Clone(candidate); // Caller cannot retain a live mutable reference.
            dirty = true;
            timer.Change(debounce, Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke(this, new(section));
    }

    public void Flush()
    {
        lock (sync) { ThrowIfDisposed(); Save(); }
    }

    private void Save()
    {
        if (!dirty || IsReadOnly) return;
        files.Write(path, JsonSerializer.SerializeToUtf8Bytes(document, typeInfo));
        dirty = false;
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void SaveFromTimer()
    {
        lock (sync)
        {
            if (disposed) return;
            try { Save(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warnings.Add("Settings save failed: " + e.Message);
            }
        }
    }

    private T Load(Func<T> defaults)
    {
        T Fresh()
        {
            var fresh = defaults();
            fresh.SchemaVersion = migrations.CurrentVersion;
            SettingsValidation.Validate(fresh);
            return Clone(fresh);
        }
        if (!File.Exists(path)) return Fresh();
        JsonObject node;
        int version;
        try
        {
            node = JsonNode.Parse(File.ReadAllBytes(files.Check(path))) as JsonObject ??
                throw new JsonException("Settings must be a JSON object.");
            version = MigrationPipeline.Version(node);
        }
        catch (JsonException e) { return Recover(e, Fresh); }
        if (version > migrations.CurrentVersion)
        {
            IsReadOnly = true;
            warnings.Add($"Schema {version} is newer than supported schema {migrations.CurrentVersion}; file is read-only.");
            return Fresh();
        }
        migrations.Apply(node); // Never quarantine a file because migration code failed.
        try
        {
            var loaded = JsonSerializer.Deserialize(node, typeInfo) ?? throw new JsonException("Null settings.");
            SettingsValidation.Validate(loaded);
            dirty = version != migrations.CurrentVersion;
            return loaded;
        }
        catch (JsonException e) when (version == migrations.CurrentVersion) { return Recover(e, Fresh); }
    }

    private T Recover(JsonException error, Func<T> defaults)
    {
        var fresh = defaults();
        files.Quarantine(path);
        warnings.Add("Corrupt settings preserved beside the original file: " + error.Message);
        dirty = true;
        return fresh;
    }

    private T Clone(T value) => JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), typeInfo)!;
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            Save(); // Leave the store usable for retry if persistence fails.
            disposed = true;
            timer.Dispose();
        }
    }
}
