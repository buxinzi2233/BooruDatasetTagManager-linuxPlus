using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bdtm.Core;

public enum VideoConvertCodec
{
    Copy,
    H264,
    H265,
}

public enum FrameExtractMode
{
    All,
    ByFps,
    NativeFps,
}

public sealed class VideoInfo
{
    public double Fps { get; set; }
    public double DurationSeconds { get; set; }
    public int FrameCount { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public override string ToString()
    {
        return $"{Width}x{Height} · {Fps:0.###} fps · {DurationSeconds:0.##}s · ~{FrameCount} frames";
    }
}

public sealed class VideoProcessResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public int OutputFileCount { get; set; }
}

public sealed class VideoProcessingService
{
    private static readonly string[] SupportedVideoExtensions =
        { ".mp4", ".flv", ".mkv", ".ts", ".avi", ".webm", ".mov" };

    private readonly FfmpegLocator _locator;

    public VideoProcessingService(FfmpegLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    public static bool IsVideoFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        return SupportedVideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }

    public static double ParseFpsString(string? fpsText)
    {
        if (string.IsNullOrWhiteSpace(fpsText) || fpsText == "0/0")
            return 0;

        int slash = fpsText.IndexOf('/');
        if (slash > 0
            && double.TryParse(fpsText.AsSpan(0, slash), NumberStyles.Float, CultureInfo.InvariantCulture, out double num)
            && double.TryParse(fpsText.AsSpan(slash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double den)
            && den > 0)
            return num / den;

        if (double.TryParse(fpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out double fps) && fps > 0)
            return fps;
        return 0;
    }

    public void EnsureAvailable() => _locator.EnsureAvailable();

    public bool IsAvailable => _locator.IsAvailable;

    public async Task<VideoInfo> GetVideoInfoAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        ValidateInputPath(inputPath);
        _locator.EnsureAvailable();

        var args = new List<string>
        {
            "-hide_banner",
            "-v", "error",
            "-select_streams", "v:0",
            "-show_entries", "stream=width,height,avg_frame_rate,r_frame_rate,nb_frames",
            "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1",
            inputPath,
        };

        var result = await RunProcessAsync(_locator.FfprobeExe, args, null, cancellationToken).ConfigureAwait(false);
        var info = new VideoInfo();
        foreach (string rawLine in (result.StdOut ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq];
            string value = line[(eq + 1)..];
            switch (key)
            {
                case "width" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w):
                    info.Width = w; break;
                case "height" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h):
                    info.Height = h; break;
                case "avg_frame_rate":
                case "r_frame_rate":
                    if (info.Fps <= 0)
                    {
                        double parsed = ParseFpsString(value);
                        if (parsed > 0) info.Fps = parsed;
                    }
                    break;
                case "nb_frames" when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) && n > 0:
                    info.FrameCount = (int)Math.Min(int.MaxValue, n); break;
                case "duration" when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d > 0:
                    info.DurationSeconds = d; break;
            }
        }

        if (info.Fps <= 0) info.Fps = 24;
        if (info.FrameCount <= 0 && info.DurationSeconds > 0 && info.Fps > 0)
            info.FrameCount = Math.Max(1, (int)Math.Round(info.DurationSeconds * info.Fps));
        return info;
    }

    public async Task<VideoProcessResult> ExtractFramesAsync(
        string inputPath,
        string outputDirectory,
        FrameExtractMode mode,
        double fps,
        string imageFormat = "png",
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateInputPath(inputPath);
        _locator.EnsureAvailable();
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);
        string ext = NormalizeImageExtension(imageFormat);
        string pattern = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(inputPath) + "_frame_%06d." + ext);

        return mode switch
        {
            FrameExtractMode.All => await ExtractAllFramesAsync(inputPath, pattern, progress, cancellationToken).ConfigureAwait(false),
            FrameExtractMode.ByFps => await ExtractByFpsAsync(inputPath, pattern, fps, progress, cancellationToken).ConfigureAwait(false),
            FrameExtractMode.NativeFps => await ExtractByFpsAsync(
                inputPath,
                pattern,
                (await GetVideoInfoAsync(inputPath, cancellationToken).ConfigureAwait(false)).Fps is var nf && nf > 0 ? nf : (fps > 0 ? fps : 1),
                progress,
                cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    public async Task<VideoProcessResult> ConvertAsync(
        string inputPath,
        string outputPath,
        VideoConvertCodec codec,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateInputPath(inputPath);
        _locator.EnsureAvailable();
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path is required.", nameof(outputPath));

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var args = new List<string> { "-hide_banner", "-y", "-i", inputPath };
        switch (codec)
        {
            case VideoConvertCodec.Copy:
                args.AddRange(new[] { "-c", "copy" });
                break;
            case VideoConvertCodec.H265:
                args.AddRange(new[] { "-c:v", "libx265", "-c:a", "aac" });
                break;
            default:
                args.AddRange(new[] { "-c:v", "libx264", "-c:a", "aac" });
                break;
        }
        args.Add(outputPath);

        var result = await RunFfmpegAsync(args, outputPath, progress, cancellationToken).ConfigureAwait(false);
        if (result.Success && File.Exists(outputPath))
            result.OutputFileCount = 1;
        return result;
    }

    private async Task<VideoProcessResult> ExtractAllFramesAsync(
        string inputPath, string outputPattern, IProgress<string>? progress, CancellationToken ct)
    {
        var args = new List<string> { "-hide_banner", "-y", "-i", inputPath, outputPattern };
        var result = await RunFfmpegAsync(args, Path.GetDirectoryName(outputPattern), progress, ct).ConfigureAwait(false);
        if (result.Success)
            result.OutputFileCount = CountMatchingFiles(Path.GetDirectoryName(outputPattern), Path.GetFileName(outputPattern));
        return result;
    }

    private async Task<VideoProcessResult> ExtractByFpsAsync(
        string inputPath, string outputPattern, double fps, IProgress<string>? progress, CancellationToken ct)
    {
        if (fps <= 0)
            throw new ArgumentOutOfRangeException(nameof(fps), "fps must be > 0");

        var args = new List<string>
        {
            "-hide_banner", "-y", "-i", inputPath,
            "-vf", "fps=" + fps.ToString(CultureInfo.InvariantCulture),
            outputPattern,
        };
        var result = await RunFfmpegAsync(args, Path.GetDirectoryName(outputPattern), progress, ct).ConfigureAwait(false);
        if (result.Success)
            result.OutputFileCount = CountMatchingFiles(Path.GetDirectoryName(outputPattern), Path.GetFileName(outputPattern));
        return result;
    }

    private async Task<VideoProcessResult> RunFfmpegAsync(
        IList<string> arguments, string? outputHint, IProgress<string>? progress, CancellationToken ct)
    {
        var processResult = await RunProcessAsync(_locator.FfmpegExe, arguments, progress, ct).ConfigureAwait(false);
        return new VideoProcessResult
        {
            Success = processResult.ExitCode == 0,
            OutputPath = outputHint,
            ErrorMessage = processResult.ExitCode == 0 ? null : processResult.GetErrorSummary(),
        };
    }

    private static async Task<ProcessExecutionResult> RunProcessAsync(
        string executable,
        IList<string> arguments,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderr = new StringBuilder();
        var stdout = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            stderr.AppendLine(e.Data);
            progress?.Report(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start process: " + executable);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { /* ignore */ }
            throw;
        }

        return new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout.ToString(),
            StdErr = stderr.ToString(),
        };
    }

    private static void ValidateInputPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("Input path is required.", nameof(inputPath));
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);
        if (!IsVideoFile(inputPath))
            throw new ArgumentException("Unsupported video file.", nameof(inputPath));
    }

    private static string NormalizeImageExtension(string? imageFormat)
    {
        string ext = (imageFormat ?? "png").Trim().TrimStart('.').ToLowerInvariant();
        return ext is "jpg" or "jpeg" ? "jpg" : "png";
    }

    private static int CountMatchingFiles(string? directory, string? patternFileName)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return 0;
        string search = Path.GetFileName(patternFileName)?.Replace("%06d", "*") ?? "*";
        return Directory.GetFiles(directory, search).Length;
    }

    private sealed class ProcessExecutionResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; } = string.Empty;
        public string StdErr { get; set; } = string.Empty;

        public string GetErrorSummary()
        {
            string[] lines = StdErr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return "FFmpeg/ffprobe process failed.";
            return lines[^1];
        }
    }
}
