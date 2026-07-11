using System.Collections.ObjectModel;
using System.Linq;
using Bdtm.Core;
using Bdtm.Onnx;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class PreviewTagRow : ObservableObject
{
    public PreviewTagRow(string tag, string chinese, float confidence, bool isSelected = true)
    {
        Tag = tag;
        Chinese = chinese;
        Confidence = confidence;
        IsSelected = isSelected;
    }

    public string Tag { get; }
    public string Chinese { get; }
    public float Confidence { get; }

    [ObservableProperty] private bool isSelected;
}

public partial class OnnxPreviewViewModel : ViewModelBase
{
    public OnnxPreviewViewModel(
        string imageName,
        IReadOnlyList<TagPrediction> predictions,
        ChineseTagLookup zhLookup,
        TagWriteMode writeMode,
        double elapsedMs,
        OnnxExecutionProvider provider)
    {
        ImageName = imageName;
        WriteModeText = writeMode == TagWriteMode.ReplaceAll ? "替换全部标签" : "追加新标签";
        Summary = $"{predictions.Count} 个候选 · {elapsedMs:F0} ms · {provider}";
        foreach (var p in predictions.OrderByDescending(x => x.Confidence))
        {
            string tag = (p.Tag ?? string.Empty).Trim().Replace(' ', '_');
            if (string.IsNullOrEmpty(tag)) continue;
            Tags.Add(new PreviewTagRow(tag, zhLookup.GetChinese(tag), p.Confidence, isSelected: true));
        }
    }

    public ObservableCollection<PreviewTagRow> Tags { get; } = new();
    public string ImageName { get; }
    public string WriteModeText { get; }
    public string Summary { get; }

    public bool Confirmed { get; private set; }

    public IReadOnlyList<TagPrediction> GetSelectedPredictions() =>
        Tags.Where(t => t.IsSelected)
            .Select(t => new TagPrediction { Tag = t.Tag, Confidence = t.Confidence })
            .ToList();

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var t in Tags) t.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var t in Tags) t.IsSelected = false;
    }

    public void MarkConfirmed() => Confirmed = true;
}
