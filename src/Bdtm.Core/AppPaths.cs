namespace Bdtm.Core;

/// <summary>
/// XDG-ish portable paths for Linux desktop; falls back sensibly on other OSes.
/// </summary>
public static class AppPaths
{
    public const string AppName = "bdtm";

    public static string ConfigDir
    {
        get
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
                return Path.Combine(xdg, AppName);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);
        }
    }

    public static string DataDir
    {
        get
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
                return Path.Combine(xdg, AppName);
            // Linux default ~/.local/share
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localShare = Path.Combine(home, ".local", "share", AppName);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                return localShare;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName);
        }
    }

    public static string SettingsFilePath => Path.Combine(ConfigDir, "settings.json");

    public static string DefaultModelsDir => Path.Combine(DataDir, "Models");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(DefaultModelsDir);
    }
}
