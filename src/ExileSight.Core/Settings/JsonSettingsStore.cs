using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

/// <summary>One owner per file. Thread-safe within that owner. Events run on the updating caller.
/// Dispose flushes pending changes. Timer failures remain dirty and are exposed through Warnings;
/// explicit Flush retries and throws on failure. Newer schemas are displayed where readable but never written.</summary>
public sealed class JsonSettingsStore<T> : ITransactionalSettingsStore<T> where T : VersionedDocument
{
    private readonly object sync = new();
    private readonly RootedFiles files;
    private readonly string path;
    private readonly JsonTypeInfo<T> typeInfo;
    private readonly MigrationPipeline migrations;
    private readonly Game? expectedGame;
    private readonly TimeSpan debounce;
    private readonly Timer timer;
    private readonly List<string> warnings = [];
    private T document;
    private bool dirty;
    private bool disposed;
    private bool rotateBackup;
    private long? firstPending;
    private static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(2);

    public JsonSettingsStore(IAppPaths paths, string path, JsonTypeInfo<T> typeInfo, Func<T> defaults,
        MigrationPipeline? migrations = null, TimeSpan? debounce = null, Action<string, string, string?>? replaceOperation = null,
        Game? expectedGame = null, Action<Stream, byte[]>? temporaryWriteOperation = null)
    {
        files = new(paths.RootDirectory, replaceOperation, temporaryWriteOperation);
        this.expectedGame = expectedGame;
        this.path = files.Check(path);
        this.typeInfo = typeInfo;
        this.migrations = migrations ?? new();
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(250);
        if (this.debounce < TimeSpan.Zero || this.debounce.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(debounce));
        var loaded = new SettingsReader<T>(files, this.path, typeInfo, this.migrations, warnings, expectedGame).Load(defaults);
        document = loaded.Value;
        LoadedDefaults = loaded.Defaults;
        SettingsValidation.DisableUnknownBindings(Clone(document), warnings);
        IsReadOnly = loaded.ReadOnly;
        dirty = loaded.Dirty;
        rotateBackup = loaded.RotateBackup;
        files.CleanTemporaryFiles(this.path);
        timer = new Timer(_ => SaveFromTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (dirty) ScheduleSave();
    }

    public bool LoadedDefaults { get; private set; }
    public bool IsReadOnly { get; private set; }
    public bool IsReadOnlyNewerSchema => IsReadOnly;
    public IReadOnlyList<string> Warnings { get { lock (sync) return warnings.ToArray(); } }
    /// <summary>Runtime view: unknown actions are disabled only in this detached snapshot.
    /// Update edits the persisted configuration, retaining unknown action kinds and enabled flags.</summary>
    public T Snapshot
    {
        get
        {
            lock (sync)
            {
                ThrowIfDisposed();
                var snapshot = Clone(document);
                SettingsValidation.DisableUnknownBindings(snapshot, warnings);
                return snapshot;
            }
        }
    }
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
            SettingsValidation.PreserveUnknownBindings(document, candidate);
            SettingsValidation.Validate(candidate, expectedGame, warnings);
            SettingsValidation.DisableUnknownBindings(Clone(candidate), warnings);
            document = Clone(candidate); // Caller cannot retain a live mutable reference.
            dirty = true;
            ScheduleSave();
        }
        Changed?.Invoke(this, new(section));
    }

    /// <summary>Persist a detached candidate before publishing it. Failure leaves the live
    /// document, pending saves and Changed subscribers untouched.</summary>
    public void Commit(string section, Action<T> update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(update);
        lock (sync)
        {
            ThrowIfDisposed();
            if (IsReadOnly) throw new InvalidOperationException("Settings use a newer schema and are read-only.");
            var candidate = Clone(document);
            var candidateWarnings = new List<string>(warnings);
            update(candidate);
            candidate.SchemaVersion = migrations.CurrentVersion;
            SettingsValidation.PreserveUnknownBindings(document, candidate);
            SettingsValidation.Validate(candidate, expectedGame, candidateWarnings);
            candidate = Clone(candidate);
            SettingsValidation.DisableUnknownBindings(Clone(candidate), candidateWarnings);
            Write(candidate);
            document = candidate;
            LoadedDefaults = false;
            warnings.Clear();
            warnings.AddRange(candidateWarnings);
            dirty = false;
            firstPending = null;
            timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
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
        Write(document);
        dirty = false;
        LoadedDefaults = false;
        firstPending = null;
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void Write(T value)
    {
        try { files.Write(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), rotateBackup); }
        catch (Exception error)
        {
            warnings.Add("Settings save failed: " + error.Message);
            throw;
        }
    }

    private void SaveFromTimer()
    {
        lock (sync)
        {
            if (disposed) return;
            try { Save(); }
            catch (Exception) { /* Write records the warning; retain dirty state for retry. */ }
        }
    }

    private void ScheduleSave()
    {
        firstPending ??= Stopwatch.GetTimestamp();
        var remaining = MaximumWait - Stopwatch.GetElapsedTime(firstPending.Value);
        var delay = remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining < debounce ? remaining : debounce;
        timer.Change(delay, Timeout.InfiniteTimeSpan);
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
