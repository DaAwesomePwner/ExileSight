using ExileSight.Core.Settings;

namespace ExileSight.Core.Setup;

public static class SetupCommit
{
    public static void Finish(ITransactionalSettingsStore<SettingsDocument> settings, string path, ExileSight.Platform.Abstractions.Install.ILocalPathResolver? resolver = null)
    {
        if (ClientLogDiscovery.Validate(path, resolver) is { } error) throw new InvalidOperationException(error);
        settings.Commit("general", s =>
        {
            s.General.ClientTxtPath = path;
            s.General.FirstRunCompleted = true;
        });
    }
}
