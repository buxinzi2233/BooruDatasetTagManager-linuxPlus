using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class BgRemovalViewModel : ViewModelBase
{
    [ObservableProperty] private string aiApiEndpoint = "http://127.0.0.1:50051";
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private List<string> models = new();
    [ObservableProperty] private string? selectedModel;
    [ObservableProperty] private string statusMessage = "请先检查连接。";
    [ObservableProperty] private bool canConnect = true;
    [ObservableProperty] private bool isBusy;

    public int? ResultCount { get; private set; }

    [RelayCommand]
    async Task CheckConnection()
    {
        if (IsBusy) return;
        IsBusy = true;
        CanConnect = false;
        StatusMessage = "正在检查连接…";
        try
        {
            using var client = new RmbgClient(AiApiEndpoint);
            var (ok, err) = await client.CheckConnectionAsync();
            if (!ok)
            {
                StatusMessage = "连接失败: " + err;
                IsConnected = false;
                return;
            }

            var rmbgModels = await client.GetRmbgModelsAsync();
            Models = rmbgModels;
            SelectedModel = Models.FirstOrDefault();
            IsConnected = true;
            StatusMessage = $"已连接，找到 {Models.Count} 个去背景模型。";
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
    /// Run background removal on a list of image paths using the provided client.
    /// Results are saved as _bgremoved.png alongside the original files.
    /// </summary>
    public async Task<bool> RunOnImagesAsync(RmbgClient client, List<string> imagePaths, Action<string> onProgress)
    {
        int done = 0;
        int failed = 0;
        foreach (var path in imagePaths)
        {
            var result = await client.RemoveBackgroundAsync(path, SelectedModel ?? Models[0]);
            if (result.Success && result.ImageData is not null)
            {
                string outPath = Path.Combine(
                    Path.GetDirectoryName(path)!,
                    Path.GetFileNameWithoutExtension(path) + "_bgremoved.png");
                await File.WriteAllBytesAsync(outPath, result.ImageData);
                done++;
                onProgress?.Invoke($"已处理 {done}/{imagePaths.Count}: {Path.GetFileName(path)}");
            }
            else
            {
                failed++;
            }
        }

        ResultCount = done;
        return done > 0;
    }
}
