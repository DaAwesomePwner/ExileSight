using ExileSight.Core.Games;
using ExileSight.Platform.Abstractions.Storage;

namespace ExileSight.Core.Settings;

/// <summary>One catalog owner per game. Stable generated IDs are filenames; names are display-only.
/// Delete removes membership, retaining the JSON as an archive. Catalog writes follow profile creation,
/// so interruption can leave an unlisted profile but never a catalog entry pointing to an unwritten file.</summary>
public sealed class ProfileStore : IProfileStore
{
    private readonly object sync = new();
    private readonly IAppPaths paths;
    private readonly string directory;
    private readonly JsonSettingsStore<ProfileCatalog> catalog;

    public ProfileStore(IAppPaths paths, Game game)
    {
        this.paths = paths;
        directory = paths.GetSettingsDirectory(game switch
        {
            Game.PoE1 => "poe1", Game.PoE2 => "poe2",
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        });
        catalog = new(paths, Path.Combine(directory, "profiles.json"), SettingsJsonContext.Default.ProfileCatalog, () => new());
    }

    public IReadOnlyList<ProfileInfo> Profiles => catalog.Snapshot.Profiles.ToArray();
    public string ActiveProfileId => catalog.Snapshot.ActiveProfileId;
    public IReadOnlyList<string> Warnings => catalog.Warnings;
    public bool IsReadOnly => catalog.IsReadOnly;

    /// <summary>Caller owns/disposes the returned store; keep only one open owner for each profile.</summary>
    public ISettingsStore<SettingsDocument> Open(string id)
    {
        lock (sync)
        {
            Require(id);
            return OpenDocument(id);
        }
    }

    private JsonSettingsStore<SettingsDocument> OpenDocument(string id) =>
        new(paths, Path.Combine(directory, id + ".json"), SettingsJsonContext.Default.SettingsDocument, () => new());

    public string Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (sync)
        {
            if (catalog.IsReadOnly) throw new InvalidOperationException("Profile catalog is read-only.");
            var id = Guid.NewGuid().ToString("N");
            using (var profile = OpenDocument(id))
            {
                profile.Update("profile", _ => { });
                profile.Flush();
            }
            catalog.Update("profiles", x => x.Profiles.Add(new(id, name)));
            catalog.Flush();
            return id;
        }
    }

    public void Rename(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (sync)
        {
            Require(id);
            catalog.Update("profiles", x =>
            {
                var index = x.Profiles.FindIndex(p => p.Id == id);
                x.Profiles[index] = new(id, name);
            });
            catalog.Flush();
        }
    }

    public void Switch(string id)
    {
        lock (sync)
        {
            Require(id);
            catalog.Update("activeProfile", x => x.ActiveProfileId = id);
            catalog.Flush();
        }
    }

    public void Delete(string id)
    {
        lock (sync)
        {
            Require(id);
            if (catalog.Snapshot.Profiles.Count == 1) throw new InvalidOperationException("Cannot delete the last profile.");
            catalog.Update("profiles", x =>
            {
                x.Profiles.RemoveAll(p => p.Id == id);
                if (x.ActiveProfileId == id) x.ActiveProfileId = x.Profiles[0].Id;
            });
            catalog.Flush();
        }
    }

    private void Require(string id)
    {
        if (!catalog.Snapshot.Profiles.Any(x => x.Id == id)) throw new ArgumentException("Unknown profile.", nameof(id));
    }

    public void Dispose() { lock (sync) catalog.Dispose(); }
}
