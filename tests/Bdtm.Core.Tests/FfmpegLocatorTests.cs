using System.Runtime.InteropServices;
using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class FfmpegLocatorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _originalPath;

    public FfmpegLocatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "bdtm-ffmpeg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _originalPath = Environment.GetEnvironmentVariable("PATH");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static string ToolName(string baseName) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? baseName + ".exe" : baseName;

    private static void CreateEmptyFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
    }

    [Fact]
    public void UsesConfiguredFilePathWhenItExists()
    {
        string toolsDir = Path.Combine(_tempRoot, "pair");
        string ffmpeg = Path.Combine(toolsDir, ToolName("ffmpeg"));
        string ffprobe = Path.Combine(toolsDir, ToolName("ffprobe"));
        CreateEmptyFile(ffmpeg);
        CreateEmptyFile(ffprobe);

        // Pointing at ffmpeg file should still resolve ffprobe as sibling.
        var locator = new FfmpegLocator(_tempRoot, ffmpeg);
        Assert.Equal(ffmpeg, locator.FfmpegExe);
        Assert.Equal(ffprobe, locator.FfprobeExe);
        Assert.True(locator.IsAvailable);
    }

    [Fact]
    public void UsesConfiguredDirectoryWhenItExists()
    {
        string toolsDir = Path.Combine(_tempRoot, "tools");
        string ffmpeg = Path.Combine(toolsDir, ToolName("ffmpeg"));
        string ffprobe = Path.Combine(toolsDir, ToolName("ffprobe"));
        CreateEmptyFile(ffmpeg);
        CreateEmptyFile(ffprobe);

        var locator = new FfmpegLocator(_tempRoot, toolsDir);

        Assert.Equal(ffmpeg, locator.FfmpegExe);
        Assert.Equal(ffprobe, locator.FfprobeExe);
        Assert.True(locator.IsAvailable);
    }

    [Fact]
    public void UsesBundledPathWhenPresent()
    {
        string rid = GetExpectedRid();
        string bundledDir = Path.Combine(_tempRoot, "ThirdParty", "ffmpeg", rid);
        string ffmpeg = Path.Combine(bundledDir, ToolName("ffmpeg"));
        string ffprobe = Path.Combine(bundledDir, ToolName("ffprobe"));
        CreateEmptyFile(ffmpeg);
        CreateEmptyFile(ffprobe);

        // Isolate from system PATH so only bundled resolution applies.
        Environment.SetEnvironmentVariable("PATH", Path.Combine(_tempRoot, "empty-path"));

        var locator = new FfmpegLocator(_tempRoot, configuredPath: "");

        Assert.Equal(ffmpeg, locator.FfmpegExe);
        Assert.Equal(ffprobe, locator.FfprobeExe);
        Assert.True(locator.IsAvailable);
    }

    [Fact]
    public void FallsBackToPathEnvironment()
    {
        string pathDir = Path.Combine(_tempRoot, "on-path");
        string ffmpeg = Path.Combine(pathDir, ToolName("ffmpeg"));
        string ffprobe = Path.Combine(pathDir, ToolName("ffprobe"));
        CreateEmptyFile(ffmpeg);
        CreateEmptyFile(ffprobe);

        Environment.SetEnvironmentVariable("PATH", pathDir);

        var locator = new FfmpegLocator(
            appDirectory: Path.Combine(_tempRoot, "app-no-bundle"),
            configuredPath: "");

        Assert.Equal(ffmpeg, locator.FfmpegExe);
        Assert.Equal(ffprobe, locator.FfprobeExe);
        Assert.True(locator.IsAvailable);
    }

    [Fact]
    public void MissingToolsAreNotAvailable()
    {
        Environment.SetEnvironmentVariable("PATH", Path.Combine(_tempRoot, "empty-path"));

        var locator = new FfmpegLocator(
            appDirectory: Path.Combine(_tempRoot, "app-empty"),
            configuredPath: "");

        Assert.False(locator.IsAvailable);
        Assert.False(File.Exists(locator.FfmpegExe));
        Assert.False(File.Exists(locator.FfprobeExe));
    }

    [Fact]
    public void EnsureAvailable_ThrowsWhenMissing()
    {
        Environment.SetEnvironmentVariable("PATH", Path.Combine(_tempRoot, "empty-path"));

        var locator = new FfmpegLocator(
            appDirectory: Path.Combine(_tempRoot, "app-empty"),
            configuredPath: "");

        var ex = Assert.Throws<InvalidOperationException>(() => locator.EnsureAvailable());
        Assert.Contains("ffmpeg", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAvailable_DoesNotThrowWhenPresent()
    {
        string toolsDir = Path.Combine(_tempRoot, "ok-tools");
        CreateEmptyFile(Path.Combine(toolsDir, ToolName("ffmpeg")));
        CreateEmptyFile(Path.Combine(toolsDir, ToolName("ffprobe")));

        var locator = new FfmpegLocator(_tempRoot, toolsDir);
        locator.EnsureAvailable();
    }

    [Fact]
    public void ConfiguredPathTakesPriorityOverBundledAndPath()
    {
        string rid = GetExpectedRid();
        string bundledDir = Path.Combine(_tempRoot, "ThirdParty", "ffmpeg", rid);
        CreateEmptyFile(Path.Combine(bundledDir, ToolName("ffmpeg")));
        CreateEmptyFile(Path.Combine(bundledDir, ToolName("ffprobe")));

        string pathDir = Path.Combine(_tempRoot, "path-tools");
        CreateEmptyFile(Path.Combine(pathDir, ToolName("ffmpeg")));
        CreateEmptyFile(Path.Combine(pathDir, ToolName("ffprobe")));
        Environment.SetEnvironmentVariable("PATH", pathDir);

        string preferred = Path.Combine(_tempRoot, "preferred");
        string preferredFfmpeg = Path.Combine(preferred, ToolName("ffmpeg"));
        string preferredFfprobe = Path.Combine(preferred, ToolName("ffprobe"));
        CreateEmptyFile(preferredFfmpeg);
        CreateEmptyFile(preferredFfprobe);

        var locator = new FfmpegLocator(_tempRoot, preferred);

        Assert.Equal(preferredFfmpeg, locator.FfmpegExe);
        Assert.Equal(preferredFfprobe, locator.FfprobeExe);
    }

    private static string GetExpectedRid()
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
