using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Bdtm.Core;
using Bdtm.Onnx;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DatasetManager _dataset = new();
    private readonly string _appDir;
    private AppSettings _settings;
    private Wd14OnnxTaggerService? _tagger;

    public MainViewModel()
    {
        _appDir = AppContext.BaseDirectory;
        _settings = AppSettings.Load(_appDir);
        Images = new ObservableCollection<ImageListItem>();
        CurrentTags = new ObservableCollection<TagRow>();
        AllTags = new ObservableCollection<string>();
        StatusText = "打开数据集文件夹以开始。";
        ProviderText = "ONNX: 未加载";
    }

    public ObservableCollection<ImageListItem> Images { get; }
    public ObservableCollection<TagRow> CurrentTags { get; }
    public ObservableCollection<string> AllTags { get; }

    [ObservableProperty] private ImageListItem? selectedImage;
    [ObservableProperty] private TagRow? selectedTag;
    [ObservableProperty] private string? newTagText;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string providerText = string.Empty;
    [ObservableProperty] private string datasetPath = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double generalThreshold = 0.52;
    [ObservableProperty] private double characterThreshold = 0.85;
    [ObservableProperty] private string onnxModelRepo = "SmilingWolf/wd-eva02-large-tagger-v3";
    [ObservableProperty] private TagWriteMode writeMode = TagWriteMode.AppendNew;

    partial void OnSelectedImageChanged(ImageListItem? value)
    {
        ReloadCurrentTags();
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (IsBusy) return;
        var window = GetMainWindow();
        if (window is null) return;

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择数据集文件夹",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
            return;

        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "无法解析所选路径。";
            return;
        }

        await LoadDatasetAsync(path);
    }

    public async Task LoadDatasetAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusText = "正在加载…";
            Images.Clear();
            CurrentTags.Clear();
            AllTags.Clear();
            SelectedImage = null;

            var options = new DatasetLoadOptions
            {
                SeparatorOnLoad = _settings.SeparatorOnLoad,
                SeparatorOnSave = _settings.SeparatorOnSave,
                FixTagsOnSaveLoad = _settings.FixTagsOnSaveLoad,
                DefaultTagsFileExtension = _settings.DefaultTagsFileExtension,
                TagFileExtensions = _settings.GetTagFilesExtensions(),
                PreviewSize = _settings.PreviewSize,
            };

            bool ok = await _dataset.LoadFromFolderAsync(path, options);
            if (!ok)
            {
                StatusText = "文件夹中没有支持的图片/视频。";
                DatasetPath = string.Empty;
                return;
            }

            DatasetPath = path;
            foreach (var item in _dataset.GetDataSource())
            {
                Images.Add(new ImageListItem(item));
            }

            RebuildAllTags();
            if (Images.Count > 0)
                SelectedImage = Images[0];

            StatusText = $"已加载 {Images.Count} 项 · {path}";
        }
        catch (Exception ex)
        {
            StatusText = "加载失败: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (IsBusy) return;
        try
        {
            // Sync grid edits back into model for selected item.
            ApplyCurrentTagsToModel();
            bool saved = _dataset.SaveAll();
            StatusText = saved ? "已保存修改的标签文件。" : "没有需要保存的修改。";
            RebuildAllTags();
        }
        catch (Exception ex)
        {
            StatusText = "保存失败: " + ex.Message;
        }
    }

    [RelayCommand]
    private void AddTag()
    {
        if (SelectedImage is null) return;
        string tag = (NewTagText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(tag)) return;
        SelectedImage.Data.Tags.Add(tag, skipIfExists: true);
        NewTagText = string.Empty;
        ReloadCurrentTags();
        RebuildAllTags();
        StatusText = $"已添加标签: {tag}";
    }

    [RelayCommand]
    private void RemoveSelectedTag(TagRow? row)
    {
        if (SelectedImage is null || row is null) return;
        SelectedImage.Data.Tags.Remove(row.Tag);
        ReloadCurrentTags();
        RebuildAllTags();
    }

    [RelayCommand]
    private async Task RunOnnxOnCurrentAsync()
    {
        if (SelectedImage is null)
        {
            StatusText = "请先选择一张图片。";
            return;
        }

        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusText = "ONNX 推理中…";
            EnsureTagger();
            if (_tagger is null || !_tagger.IsModelReady(OnnxModelRepo))
            {
                StatusText = $"模型未就绪。请将 model.onnx 与 selected_tags.csv 放到 Models/{OnnxModelRepo}/";
                return;
            }

            if (!_tagger.IsLoaded || !string.Equals(_tagger.LoadedRepo, OnnxModelRepo, StringComparison.OrdinalIgnoreCase))
                await Task.Run(() => _tagger.LoadModel(OnnxModelRepo));

            ProviderText = $"ONNX: {_tagger.ActiveProvider}";
            var result = await Task.Run(() =>
                _tagger.TagImage(SelectedImage.Data.ImageFilePath, GeneralThreshold, CharacterThreshold));

            TagWriteService.ApplyTags(SelectedImage.Data, result.Tags, WriteMode, sortByConfidence: true);
            ReloadCurrentTags();
            RebuildAllTags();
            StatusText = $"打标完成 · {result.Tags.Count} tags · {result.ElapsedMilliseconds:F0} ms · {result.Provider}";
            ProviderText = $"ONNX: {result.Provider}";
        }
        catch (Exception ex)
        {
            StatusText = "ONNX 失败: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.Wd14Tagger.Threshold = GeneralThreshold;
        _settings.Wd14Tagger.CharacterThreshold = CharacterThreshold;
        _settings.Wd14Tagger.SelectedModelRepo = OnnxModelRepo;
        _settings.OnnxTaggerLastModelId = OnnxModelRepo;
        _settings.Save();
        StatusText = "设置已保存。";
    }

    private void EnsureTagger()
    {
        if (_tagger is not null) return;
        string modelsRoot = Path.Combine(_appDir, "Models");
        var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var factory = new OnnxSessionFactory(msg => logs.Enqueue(msg));
        _tagger = new Wd14OnnxTaggerService(factory, modelsRoot);
        // seed thresholds from settings
        GeneralThreshold = _settings.Wd14Tagger.Threshold;
        CharacterThreshold = _settings.Wd14Tagger.CharacterThreshold;
        if (!string.IsNullOrWhiteSpace(_settings.Wd14Tagger.SelectedModelRepo))
            OnnxModelRepo = _settings.Wd14Tagger.SelectedModelRepo;
    }

    private void ReloadCurrentTags()
    {
        CurrentTags.Clear();
        if (SelectedImage is null) return;
        foreach (var t in SelectedImage.Data.Tags.Items)
            CurrentTags.Add(new TagRow { Tag = t.Tag, Weight = t.Weight });
    }

    private void ApplyCurrentTagsToModel()
    {
        if (SelectedImage is null) return;
        // Reflect DataGrid edits (Tag/Weight) back
        var tags = CurrentTags
            .Where(t => !string.IsNullOrWhiteSpace(t.Tag))
            .Select(t => t.Tag.Trim())
            .ToList();
        SelectedImage.Data.Tags.SetTags(tags);
    }

    private void RebuildAllTags()
    {
        AllTags.Clear();
        foreach (var tag in _dataset.DataSet.Values
                     .SelectMany(i => i.Tags.Items.Select(t => t.Tag))
                     .Where(t => !string.IsNullOrWhiteSpace(t))
                     .GroupBy(t => t)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                     .Select(g => $"{g.Key} ({g.Count()})"))
        {
            AllTags.Add(tag);
        }
    }

    private static Window? GetMainWindow()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is
            global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}


public sealed class ImageListItem
{
    public ImageListItem(DatasetManager.DataItem data)
    {
        Data = data;
    }

    public DatasetManager.DataItem Data { get; }
    public string Name => Data.Name;
    public string Path => Data.ImageFilePath;
    public override string ToString() => Name;
}

public partial class TagRow : ObservableObject
{
    [ObservableProperty] private string tag = string.Empty;
    [ObservableProperty] private float weight = 1f;
}
