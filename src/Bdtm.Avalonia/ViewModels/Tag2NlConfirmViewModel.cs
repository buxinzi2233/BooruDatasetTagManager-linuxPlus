using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bdtm.Avalonia.ViewModels;

public partial class Tag2NlConfirmViewModel : ViewModelBase
{
    public Tag2NlConfirmViewModel(CaptionScanResult scan)
    {
        SummaryText =
            $"数据集根目录：{scan.SourceRoot}\n" +
            $"图片总数：{scan.Total}\n" +
            $"已有输出：{scan.Existing}\n" +
            $"默认待处理：{scan.Pending}\n" +
            $"输出目录：{scan.OutputRoot}";
    }

    public string SummaryText { get; }
    [ObservableProperty] private bool reprocessExisting;
}
