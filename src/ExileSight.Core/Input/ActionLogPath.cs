namespace ExileSight.Core.Input;

/// <summary>The audit log remains in application data, independently of the profile/settings root.</summary>
public static class ActionLogPath
{
    public static string Default => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExileSight", "logs", "actions.log");
}
