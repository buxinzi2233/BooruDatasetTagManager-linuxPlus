using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class BgRemovalViewModel : ViewModelBase
{
    [ObservableProperty] private string aiApiEndpoint = "http://127.0.0.1:50051";
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private ObservableCollection<BgModel> models = new();
    [ObservableProperty] private BgModel? selectedModel;
    [ObservableProperty] private string statusMessage = "请先检查连接。";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canConnect = true;

    // Mode: All images / Selected only
    [ObservableProperty] private bool modeAllImages = true;
    [ObservableProperty] private bool modeSelectedOnly;

    // Background: Transparent / Solid color
    [ObservableProperty] private bool bgTransparent = true;
    [ObservableProperty] private bool bgSolidColor;
    [ObservableProperty] private string solidColor = "#FFFFFF";

    // Output: Overwrite original / Save as copy
    [ObservableProperty] private bool outputOverwrite;
    [ObservableProperty] private bool outputSaveAsCopy = true;
    [ObservableProperty] private bool backupOriginal = true;

    // Test result
    [ObservableProperty] private string testResultMessage = "";

    public BgBatchSummary? BatchSummary { get; private set; }

    public BgOptions BuildOptions()
    {
        return new BgOptions
        {
            Mode = ModeSelectedOnly ? BgMode.SelectedOnly : BgMode.AllImages,
            Output = OutputOverwrite ? BgOutputMode.OverwriteOriginal : BgOutputMode.SaveAsCopy,
            Background = BgSolidColor ? BgBackgroundKind.SolidColor : BgBackgroundKind.Transparent,
            SolidColorArgb = SolidColor,
            BackupOriginal = BackupOriginal,
        };
    }

    [RelayCommand]
    async Task CheckConnection()
    {
        if (IsBusy) return;
        IsBusy = true;
        CanConnect = false;
        StatusMessage = "正在检查连接…";
        Models.Clear();
        TestResultMessage = "";

        try
        {
            var backend = new AiApiBgBackend(AiApiEndpoint);
            var modelList = await backend.ListModelsAsync();
            foreach (var m in modelList)
                Models.Add(m);
            SelectedModel = Models.FirstOrDefault();
            IsConnected = Models.Count > 0;
            StatusMessage = IsConnected
                ? $"已连接，找到 {Models.Count} 个去背景模型。"
                : "已连接，但未找到去背景模型（检查 AiApiServer 是否已加载 rmbg 模型）。";
        }
        catch (Exception ex)
        {
            StatusMessage = "连接失败: " + ex.Message;
            IsConnected = false;
        }
        finally
        {
            IsBusy = false;
            CanConnect = true;
        }
    }

    /// <summary>
    /// Test background removal on a single image path (preview).
    /// Returns true if successful.
    /// </summary>
    public async Task<bool> TestAsync(string imagePath, CancellationToken ct = default)
    {
        if (SelectedModel is null)
        {
            TestResultMessage = "请先选择一个模型。";
            return false;
        }

        TestResultMessage = "测试中…";
        IsBusy = true;
        try
        {
            var backend = new AiApiBgBackend(AiApiEndpoint);
            var result = await backend.RunAsync(SelectedModel, imagePath, BuildOptions(), ct);
            TestResultMessage = result.Success
                ? $"测试成功：{Path.GetFileName(result.OutputPath)}"
                : "测试失败: " + (result.ErrorMessage ?? "unknown");
            return result.Success;
        }
        catch (Exception ex)
        {
            TestResultMessage = "测试失败: " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Run bg removal on multiple image paths.
    /// </summary>
    public async Task<BgBatchSummary> RunBatchAsync(IReadOnlyList<string> imagePaths, IProgress<BgProgress>? progress = null, CancellationToken ct = default)
    {
        if (SelectedModel is null)
            throw new InvalidOperationException("未选择模型。");

        var backend = new AiApiBgBackend(AiApiEndpoint);
        var service = new BgRemovalService();
        BatchSummary = await service.RunOnAsync(backend, SelectedModel, BuildOptions(), imagePaths, progress, ct);
        return BatchSummary;
    }
}