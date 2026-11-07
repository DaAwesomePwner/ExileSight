using ExileSight.Core.Settings;

namespace ExileSight.Core.Setup;

public static class SetupCommit
{
    public static void Finish(ITransactionalSettingsStore<SettingsDocument> settings, string path)
    {
        if (ClientLogDiscovery.Validate(path) is { } error) throw new InvalidOperationException(error);
        settings.Commit("general", s =>
        {
            s.General.ClientTxtPath = path;
            s.General.FirstRunCompleted = true;
        });
    }
}
