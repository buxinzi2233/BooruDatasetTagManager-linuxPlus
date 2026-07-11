using System.IO;
using System.Threading;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class Tag2NlProgressViewModel : ViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private string currentFile = "—";
    [ObservableProperty] private string countsText = "成功：0    跳过：0    失败：0";
    [ObservableProperty] private double progressValue;
    [ObservableProperty] private string statusText = "准备中…";
    [ObservableProperty] private bool cancelEnabled = true;
    [ObservableProperty] private bool allowClose;

    public CancellationToken Token => _cts.Token;

    public void RequestCancel()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        CancelEnabled = false;
        StatusText = "正在取消，将在当前操作结束后停止……";
    }

    public void ApplyProgress(CaptionGenerationProgress p)
    {
        CurrentFile = string.IsNullOrEmpty(p.CurrentFile) ? "—" : Path.GetFileName(p.CurrentFile);
        CountsText = $"成功：{p.Succeeded}    跳过：{p.Skipped}    失败：{p.Failed}";
        if (p.Total > 0)
            ProgressValue = 100.0 * p.Completed / p.Total;
        StatusText = p.Stage switch
        {
            CaptionProgressStage.Scanning => "扫描中…",
            CaptionProgressStage.Processing => "处理中…",
            CaptionProgressStage.Completed => "完成",
            _ => StatusText
        };
    }

    [RelayCommand]
    private void Cancel() => RequestCancel();
}
