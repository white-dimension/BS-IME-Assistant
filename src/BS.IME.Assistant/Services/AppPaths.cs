namespace BS.IME.Assistant.Services;

internal static class AppPaths
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BS-IME-Assistant");

    public static string Root => BaseDir;
    public static string SettingsFile => Path.Combine(BaseDir, "settings.json");
    public static string SettingsBackupFile => Path.Combine(BaseDir, "settings.broken.json");
    public static string LogDirectory => Path.Combine(BaseDir, "logs");
    public static string LogFile => Path.Combine(LogDirectory, "debug.log");
}
