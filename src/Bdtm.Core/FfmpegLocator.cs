using System.Runtime.InteropServices;

namespace Bdtm.Core;

/// <summary>
/// Cross-platform discovery of ffmpeg/ffprobe executables.
/// Resolution order: configured path → bundled ThirdParty/ffmpeg/{rid}/ → PATH.
/// </summary>
public sealed class FfmpegLocator
{
    private readonly string _appDirectory;
    private readonly string _configuredPath;

    public FfmpegLocator(string appDirectory, string configuredPath)
    {
        _appDirectory = appDirectory ?? throw new ArgumentNullException(nameof(appDirectory));
        _configuredPath = configuredPath ?? string.Empty;
    }

    public string FfmpegExe => ResolveExecutable(FfmpegFileName);

    public string FfprobeExe => ResolveExecutable(FfprobeFileName);

    public bool IsAvailable => File.Exists(FfmpegExe) && File.Exists(FfprobeExe);

    public void EnsureAvailable()
    {
        if (IsAvailable)
            return;

        throw new InvalidOperationException(
            "ffmpeg/ffprobe were not found. Configure FfmpegPath, place binaries under " +
            "ThirdParty/ffmpeg/{rid}/, or install them on PATH.");
    }

    internal static string FfmpegFileName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

    internal static string FfprobeFileName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffprobe.exe" : "ffprobe";

    internal static string RuntimeRid
    {
        get
        {
            string rid = RuntimeInformation.RuntimeIdentifier;
            if (!string.IsNullOrWhiteSpace(rid))
                return rid;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }
    }

    private string ResolveExecutable(string fileName)
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath))
        {
            string? fromConfig = ResolveFromConfiguredPath(fileName);
            if (!string.IsNullOrEmpty(fromConfig))
                return fromConfig;
        }

        string bundled = Path.Combine(_appDirectory, "ThirdParty", "ffmpeg", RuntimeRid, fileName);
        if (File.Exists(bundled))
            return bundled;

        string? fromPath = FindOnPath(fileName);
        if (!string.IsNullOrEmpty(fromPath))
            return fromPath;

        return bundled;
    }

    private string? ResolveFromConfiguredPath(string fileName)
    {
        // Existing file: use only when it matches the requested tool; otherwise try sibling in same dir.
        if (File.Exists(_configuredPath))
        {
            if (IsMatchingToolFile(_configuredPath, fileName))
                return _configuredPath;

            string? dir = Path.GetDirectoryName(_configuredPath);
            if (!string.IsNullOrEmpty(dir))
            {
                string sibling = Path.Combine(dir, fileName);
                if (File.Exists(sibling))
                    return sibling;
            }

            // Configured file is the other tool and sibling missing — fall through to bundled/PATH.
            return null;
        }

        // Explicit file path (may not exist yet): return as-is when leaf matches this tool.
        if (IsExplicitFilePath(_configuredPath, fileName))
            return _configuredPath;

        // Treat as directory (existing or not).
        return Path.Combine(_configuredPath, fileName);
    }

    private static bool IsMatchingToolFile(string path, string fileName)
    {
        string leaf = Path.GetFileName(path);
        if (string.Equals(leaf, fileName, StringComparison.OrdinalIgnoreCase))
            return true;

        bool leafIsFfmpeg = leaf.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase);
        bool leafIsFfprobe = leaf.Equals("ffprobe", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase);
        bool wantFfmpeg = fileName.StartsWith("ffmpeg", StringComparison.OrdinalIgnoreCase);
        bool wantFfprobe = fileName.StartsWith("ffprobe", StringComparison.OrdinalIgnoreCase);

        return (leafIsFfmpeg && wantFfmpeg) || (leafIsFfprobe && wantFfprobe);
    }

    private static bool IsExplicitFilePath(string configuredPath, string fileName)
    {
        if (configuredPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return true;

        string leaf = Path.GetFileName(configuredPath);
        if (string.Equals(leaf, fileName, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(leaf, "ffmpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leaf, "ffprobe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leaf, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leaf, "ffprobe.exe", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? FindOnPath(string fileName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        foreach (string segment in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            string candidate = Path.Combine(segment.Trim(), fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
