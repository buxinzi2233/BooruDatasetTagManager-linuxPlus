using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class VideoToolsViewModel : ViewModelBase
{
    private readonly Func<FfmpegLocator> _locatorFactory;
    private readonly string? _initialVideoPath;
    private readonly Action<string>? _loadDataset;
    private CancellationTokenSource? _cts;

    public VideoToolsViewModel(Func<FfmpegLocator> locatorFactory, string? initialVideoPath = null, Action<string>? loadDataset = null)
    {
        _locatorFactory = locatorFactory;
        _initialVideoPath = initialVideoPath;
        _loadDataset = loadDataset;
        ExtractModes = new ObservableCollection<string> { "按 FPS 抽帧", "原生 FPS", "全部帧（慎用）" };
        SelectedExtractMode = ExtractModes[0];
        ImageFormats = new ObservableCollection<string> { "png", "jpg" };
        SelectedImageFormat = "png";
        ExtractFps = 1;
        StatusText = "选择视频文件以开始。";
        if (!string.IsNullOrWhiteSpace(initialVideoPath) && File.Exists(initialVideoPath))
            VideoPath = initialVideoPath;
        RefreshFfmpegStatus();
    }

    public ObservableCollection<string> ExtractModes { get; }
    public ObservableCollection<string> ImageFormats { get; }
    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty] private string videoPath = string.Empty;
    [ObservableProperty] private string outputDirectory = string.Empty;
    [ObservableProperty] private string videoInfoText = "—";
    [ObservableProperty] private string selectedExtractMode = string.Empty;
    [ObservableProperty] private string selectedImageFormat = "png";
    [ObservableProperty] private double extractFps = 1;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string ffmpegStatus = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastOutputDirectory;

    public string? InitialVideoPath => _initialVideoPath;

    partial void OnVideoPathChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (string.IsNullOrWhiteSpace(OutputDirectory) && File.Exists(value))
            OutputDirectory = Path.Combine(Path.GetDirectoryName(value) ?? ".", Path.GetFileNameWithoutExtension(value) + "_frames");
        _ = LoadInfoAsync();
    }

    private void RefreshFfmpegStatus()
    {
        try
        {
            var locator = _locatorFactory();
            FfmpegStatus = locator.IsAvailable
                ? $"FFmpeg: {locator.FfmpegExe}"
                : "FFmpeg 未找到（请安装 ffmpeg 或在设置中配置路径）";
        }
        catch (Exception ex)
        {
            FfmpegStatus = "FFmpeg 检测失败: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task BrowseVideoAsync()
    {
        var top = GetOwnerWindow();
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择视频",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Videos")
                {
                    Patterns = new[] { "*.mp4", "*.mkv", "*.webm", "*.mov", "*.avi", "*.flv", "*.ts" },
                },
            },
        });
        if (files.Count == 0) return;
        string? path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            VideoPath = path;
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var top = GetOwnerWindow();
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择抽帧输出目录",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        string? path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            OutputDirectory = path;
    }

    [RelayCommand]
    private async Task LoadInfoAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(VideoPath) || !File.Exists(VideoPath))
        {
            VideoInfoText = "—";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "读取视频信息…";
            var locator = _locatorFactory();
            var svc = new VideoProcessingService(locator);
            var info = await svc.GetVideoInfoAsync(VideoPath);
            VideoInfoText = info.ToString();
            StatusText = "视频信息已更新。";
            AppendLog(VideoInfoText);
        }
        catch (Exception ex)
        {
            VideoInfoText = "读取失败";
            StatusText = "读取失败: " + ex.Message;
            AppendLog(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExtractAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(VideoPath) || !File.Exists(VideoPath))
        {
            StatusText = "请先选择有效视频文件。";
            return;
        }
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            StatusText = "请指定输出目录。";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            IsBusy = true;
            StatusText = "抽帧中…";
            AppendLog("开始抽帧 → " + OutputDirectory);

            var locator = _locatorFactory();
            var svc = new VideoProcessingService(locator);
            var mode = SelectedExtractMode switch
            {
                "全部帧（慎用）" => FrameExtractMode.All,
                "原生 FPS" => FrameExtractMode.NativeFps,
                _ => FrameExtractMode.ByFps,
            };

            var progress = new Progress<string>(line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                // Keep UI responsive; only show last-ish progress snippets.
                if (line.Contains("frame=", StringComparison.Ordinal) || line.Contains("time=", StringComparison.Ordinal))
                    StatusText = line.Trim();
            });

            var result = await svc.ExtractFramesAsync(
                VideoPath,
                OutputDirectory,
                mode,
                ExtractFps,
                SelectedImageFormat,
                progress,
                ct);

            if (result.Success)
            {
                LastOutputDirectory = result.OutputPath ?? OutputDirectory;
                StatusText = $"抽帧完成 · {result.OutputFileCount} 张 → {LastOutputDirectory}";
                AppendLog(StatusText);
            }
            else
            {
                StatusText = "抽帧失败: " + (result.ErrorMessage ?? "unknown");
                AppendLog(StatusText);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消。";
            AppendLog(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = "抽帧失败: " + ex.Message;
            AppendLog(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        string? dir = LastOutputDirectory ?? OutputDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            StatusText = "输出目录不存在。";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText = "无法打开目录: " + ex.Message;
        }
    }

    /// <summary>Ask host main window to open the frame output folder as a dataset.</summary>
    [RelayCommand]
    private void LoadOutputAsDataset()
    {
        string? dir = LastOutputDirectory ?? OutputDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            StatusText = "没有可加载的输出目录（请先抽帧）。";
            return;
        }

        if (_loadDataset is null)
        {
            StatusText = "当前无法回调主窗口加载数据集。";
            return;
        }

        _loadDataset(dir);
        StatusText = "已请求主窗口加载: " + dir;
        AppendLog(StatusText);
    }

    private void AppendLog(string line)
    {
        LogLines.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + line);
        while (LogLines.Count > 100)
            LogLines.RemoveAt(LogLines.Count - 1);
    }

    private static global::Avalonia.Controls.Window? GetOwnerWindow()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is
            global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}

