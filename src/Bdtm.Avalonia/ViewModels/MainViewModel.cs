using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Bdtm.Avalonia.Services;
using Bdtm.Core;
using Bdtm.Onnx;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DatasetManager _dataset = new();
    private readonly string _appDir;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _onnxLogs = new();
    private AppSettings _settings;
    private ChineseTagLookup _zhLookup = ChineseTagLookup.Empty;
    private Wd14OnnxTaggerService? _tagger;
    private string _modelsRoot = string.Empty;
    private CancellationTokenSource? _previewCts;
    private int _thumbGen;

    public MainViewModel()
    {
        _appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _settings = AppSettings.Load(_appDir);
        Images = new ObservableCollection<ImageListItem>();
        CurrentTags = new ObservableCollection<TagRow>();
        GlobalTags = new ObservableCollection<TagCountRow>();

        if (!string.IsNullOrWhiteSpace(_settings.Wd14Tagger.SelectedModelRepo))
            OnnxModelRepo = _settings.Wd14Tagger.SelectedModelRepo;
        else if (!string.IsNullOrWhiteSpace(_settings.OnnxTaggerLastModelId))
            OnnxModelRepo = _settings.OnnxTaggerLastModelId;

        GeneralThreshold = _settings.Wd14Tagger.Threshold;
        CharacterThreshold = _settings.Wd14Tagger.CharacterThreshold;

        LoadChineseLookup();
        _modelsRoot = ResolveModelsRoot();
        RefreshOnnxStatus(prefix: "启动");
        StatusText = $"模型目录: {_modelsRoot} · 中文词表: {_zhLookup.Count}";
    }

    public ObservableCollection<ImageListItem> Images { get; }
    public ObservableCollection<TagRow> CurrentTags { get; }
    public ObservableCollection<TagCountRow> GlobalTags { get; }

    [ObservableProperty] private ImageListItem? selectedImage;
    [ObservableProperty] private TagRow? selectedTag;
    [ObservableProperty] private TagCountRow? selectedGlobalTag;
    [ObservableProperty] private string? newTagText;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string providerText = string.Empty;
    [ObservableProperty] private string datasetPath = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private double generalThreshold = 0.52;
    [ObservableProperty] private double characterThreshold = 0.85;
    [ObservableProperty] private string onnxModelRepo = "SmilingWolf/wd-eva02-large-tagger-v3";
    [ObservableProperty] private TagWriteMode writeMode = TagWriteMode.AppendNew;
    [ObservableProperty] private Bitmap? previewImage;
    [ObservableProperty] private bool showPaths;

    partial void OnSelectedImageChanged(ImageListItem? value)
    {
        ReloadCurrentTags();
        _ = UpdatePreviewAsync(value);
    }

    partial void OnOnnxModelRepoChanged(string value) => RefreshOnnxStatus();

    partial void OnShowPathsChanged(bool value)
    {
        foreach (var item in Images)
            item.ShowFullPath = value;
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
            await RunOnUiAsync(() =>
            {
                IsBusy = true;
                StatusText = "正在加载…";
                Images.Clear();
                CurrentTags.Clear();
                GlobalTags.Clear();
                SelectedImage = null;
                var prev = PreviewImage;
                PreviewImage = null;
                prev?.Dispose();
            });

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
                await RunOnUiAsync(() =>
                {
                    StatusText = "文件夹中没有支持的图片/视频。";
                    DatasetPath = string.Empty;
                });
                return;
            }

            bool showPaths = ShowPaths;
            var items = _dataset.GetDataSource()
                .Select(d => new ImageListItem(d) { ShowFullPath = showPaths })
                .ToList();

            await RunOnUiAsync(() =>
            {
                DatasetPath = path;
                foreach (var item in items)
                    Images.Add(item);
                RebuildGlobalTags();
                if (Images.Count > 0)
                    SelectedImage = Images[0];
                StatusText = $"已加载 {Images.Count} 项 · {path}";
                RefreshOnnxStatus();
            });

            _ = LoadThumbnailsAsync();
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() => StatusText = "加载失败: " + ex.Message);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (IsBusy) return;
        try
        {
            ApplyCurrentTagsToModel();
            bool saved = _dataset.SaveAll();
            StatusText = saved ? "已保存修改的标签文件。" : "没有需要保存的修改。";
            RebuildGlobalTags();
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
        RebuildGlobalTags();
        StatusText = $"已添加标签: {tag}";
    }

    [RelayCommand]
    private void RemoveSelectedTag(TagRow? row)
    {
        var target = row ?? SelectedTag;
        if (SelectedImage is null || target is null) return;
        SelectedImage.Data.Tags.Remove(target.Tag);
        ReloadCurrentTags();
        RebuildGlobalTags();
    }

    [RelayCommand]
    private void AddGlobalTagToCurrent()
    {
        if (SelectedImage is null || SelectedGlobalTag is null) return;
        SelectedImage.Data.Tags.Add(SelectedGlobalTag.Tag, skipIfExists: true);
        ReloadCurrentTags();
        RebuildGlobalTags();
        StatusText = $"已添加: {SelectedGlobalTag.Tag}";
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
            StatusText = "ONNX 加载/推理中…";
            EnsureTagger();

            if (_tagger is null)
            {
                StatusText = "ONNX 服务未初始化。";
                RefreshOnnxStatus();
                return;
            }

            if (!_tagger.IsModelReady(OnnxModelRepo))
            {
                string expected = Wd14OnnxTaggerService.GetLocalPath(_modelsRoot, OnnxModelRepo, Wd14OnnxTaggerService.ModelFileName);
                StatusText = $"模型文件未找到。期望: {expected}";
                RefreshOnnxStatus();
                return;
            }

            if (!_tagger.IsLoaded || !string.Equals(_tagger.LoadedRepo, OnnxModelRepo, StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "正在加载 ONNX 会话（首次可能较慢）…";
                await Task.Run(() => _tagger.LoadModel(OnnxModelRepo));
                FlushOnnxLogsToStatus();
            }

            ProviderText = FormatProviderText(sessionLoaded: true);
            var result = await Task.Run(() =>
                _tagger.TagImage(SelectedImage.Data.ImageFilePath, GeneralThreshold, CharacterThreshold));

            TagWriteService.ApplyTags(SelectedImage.Data, result.Tags, WriteMode, sortByConfidence: true);
            ReloadCurrentTags();
            RebuildGlobalTags();
            StatusText = $"打标完成 · {result.Tags.Count} tags · {result.ElapsedMilliseconds:F0} ms · {result.Provider}";
            ProviderText = FormatProviderText(sessionLoaded: true);
            if (result.Provider == OnnxExecutionProvider.Cpu && !string.IsNullOrWhiteSpace(_tagger.FallbackReason))
                StatusText += " | CUDA 回退: " + _tagger.FallbackReason;
        }
        catch (Exception ex)
        {
            StatusText = "ONNX 失败: " + ex.Message;
            FlushOnnxLogsToStatus();
            RefreshOnnxStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RefreshOnnxStatusUi()
    {
        _modelsRoot = ResolveModelsRoot();
        if (_tagger is not null)
        {
            _tagger.Dispose();
            _tagger = null;
        }
        RefreshOnnxStatus(prefix: "刷新");
        StatusText = $"模型目录: {_modelsRoot} · 中文词表: {_zhLookup.Count}";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.Wd14Tagger.Threshold = GeneralThreshold;
        _settings.Wd14Tagger.CharacterThreshold = CharacterThreshold;
        _settings.Wd14Tagger.SelectedModelRepo = OnnxModelRepo;
        _settings.OnnxTaggerLastModelId = OnnxModelRepo;
        _settings.ModelsPath = _modelsRoot;
        _settings.Save();
        StatusText = "设置已保存。";
    }

    private void LoadChineseLookup()
    {
        string[] candidates =
        {
            Path.Combine(_appDir, "Data", "danbooru-0-zh.csv"),
            Path.GetFullPath(Path.Combine(_appDir, "..", "..", "..", "Data", "danbooru-0-zh.csv")),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/BooruDatasetTagManager-linuxPlus/BooruDatasetTagManager/Data/danbooru-0-zh.csv"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/BooruDatasetTagManager-linuxPlus/src/Bdtm.Avalonia/Data/danbooru-0-zh.csv"),
        };

        foreach (string c in candidates)
        {
            if (!File.Exists(c)) continue;
            _zhLookup = ChineseTagLookup.LoadFromFile(c, fixTags: true);
            if (_zhLookup.Count > 0)
                return;
        }

        _zhLookup = ChineseTagLookup.Empty;
    }

    private async Task LoadThumbnailsAsync()
    {
        int gen = Interlocked.Increment(ref _thumbGen);
        int edge = Math.Clamp(_settings.PreviewSize, 48, 256);
        var snapshot = Images.ToList();
        // Sequential is safer for memory; yield so UI stays responsive.
        foreach (var item in snapshot)
        {
            if (gen != _thumbGen) return;
            if (!ImageThumbnailLoader.IsRasterImage(item.Path))
                continue;

            try
            {
                var bmp = await ImageThumbnailLoader.LoadAsync(item.Path, edge);
                if (gen != _thumbGen)
                {
                    bmp?.Dispose();
                    return;
                }
                if (bmp is null) continue;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var old = item.Thumbnail;
                    item.Thumbnail = bmp;
                    old?.Dispose();
                });
                await Task.Yield();
            }
            catch
            {
                // skip bad images
            }
        }
    }

    private async Task UpdatePreviewAsync(ImageListItem? item)
    {
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        if (item is null || !ImageThumbnailLoader.IsRasterImage(item.Path))
        {
            await RunOnUiAsync(() =>
            {
                var prevNull = PreviewImage;
                PreviewImage = null;
                prevNull?.Dispose();
            });
            return;
        }

        try
        {
            var bmp = await ImageThumbnailLoader.LoadAsync(item.Path, maxEdge: 720, ct);
            if (ct.IsCancellationRequested)
            {
                bmp?.Dispose();
                return;
            }

            await RunOnUiAsync(() =>
            {
                var previous = PreviewImage;
                PreviewImage = bmp;
                previous?.Dispose();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await RunOnUiAsync(() =>
            {
                var prev = PreviewImage;
                PreviewImage = null;
                prev?.Dispose();
            });
        }
    }

    private static Task RunOnUiAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    private void EnsureTagger()
    {
        _modelsRoot = ResolveModelsRoot();
        if (_tagger is not null)
            return;

        var factory = new OnnxSessionFactory(msg =>
        {
            _onnxLogs.Enqueue(msg);
            System.Diagnostics.Debug.WriteLine(msg);
        });
        _tagger = new Wd14OnnxTaggerService(factory, _modelsRoot);
    }

    private string ResolveModelsRoot()
    {
        string? env = Environment.GetEnvironmentVariable("BDTM_MODELS_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        if (!string.IsNullOrWhiteSpace(_settings.ModelsPath))
        {
            string configured = _settings.ModelsPath;
            if (!Path.IsPathRooted(configured))
                configured = Path.GetFullPath(Path.Combine(_appDir, configured));
            if (Directory.Exists(configured))
                return configured;
        }

        string besideApp = Path.Combine(_appDir, "Models");
        if (IsRepoPresent(besideApp, OnnxModelRepo) || Directory.Exists(besideApp))
            return besideApp;

        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/toolbox/datasets/Tool/sd-image-sorter/data/models/wd14-tagger"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/toolbox/model-train/AnimaLoraStudio/models/wd14"),
        };

        foreach (string c in candidates)
        {
            if (!Directory.Exists(c)) continue;
            if (IsRepoPresent(c, OnnxModelRepo))
                return c;

            string flat = Path.Combine(c, OnnxModelRepo.Split('/').Last());
            if (File.Exists(Path.Combine(flat, "model.onnx")) && File.Exists(Path.Combine(flat, "selected_tags.csv")))
            {
                TryLinkFlatModelInto(besideApp, OnnxModelRepo, flat);
                if (IsRepoPresent(besideApp, OnnxModelRepo))
                    return besideApp;
            }

            string underscored = OnnxModelRepo.Replace('/', '_');
            string underPath = Path.Combine(c, underscored);
            if (File.Exists(Path.Combine(underPath, "model.onnx")))
            {
                TryLinkFlatModelInto(besideApp, OnnxModelRepo, underPath);
                if (IsRepoPresent(besideApp, OnnxModelRepo))
                    return besideApp;
            }
        }

        Directory.CreateDirectory(besideApp);
        return besideApp;
    }

    private static void TryLinkFlatModelInto(string modelsRoot, string repo, string sourceDir)
    {
        try
        {
            string dest = Path.GetFullPath(Path.Combine(modelsRoot, repo.Replace('/', Path.DirectorySeparatorChar)));
            if (Directory.Exists(dest) || File.Exists(dest))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (Directory.Exists(sourceDir))
                Directory.CreateSymbolicLink(dest, sourceDir);
        }
        catch
        {
        }
    }

    private static bool IsRepoPresent(string modelsRoot, string repo)
    {
        string model = Wd14OnnxTaggerService.GetLocalPath(modelsRoot, repo, Wd14OnnxTaggerService.ModelFileName);
        string labels = Wd14OnnxTaggerService.GetLocalPath(modelsRoot, repo, Wd14OnnxTaggerService.LabelsFileName);
        return File.Exists(model) && File.Exists(labels);
    }

    private void RefreshOnnxStatus(string? prefix = null)
    {
        bool filesReady = IsRepoPresent(_modelsRoot, OnnxModelRepo);
        bool sessionLoaded = _tagger?.IsLoaded == true
            && string.Equals(_tagger.LoadedRepo, OnnxModelRepo, StringComparison.OrdinalIgnoreCase);

        ProviderText = filesReady
            ? (sessionLoaded
                ? FormatProviderText(sessionLoaded: true)
                : "ONNX: 模型已就绪（点「ONNX 当前图」后加载到 GPU/CPU）")
            : "ONNX: 模型文件缺失";

        if (prefix is not null && !filesReady)
        {
            string expected = Wd14OnnxTaggerService.GetLocalPath(_modelsRoot, OnnxModelRepo, Wd14OnnxTaggerService.ModelFileName);
            StatusText = $"{prefix}: 未找到模型。期望路径: {expected}";
        }
    }

    private string FormatProviderText(bool sessionLoaded)
    {
        if (_tagger is null || !sessionLoaded)
            return "ONNX: 模型已就绪（点「ONNX 当前图」后加载到 GPU/CPU）";

        if (_tagger.ActiveProvider == OnnxExecutionProvider.Cuda)
            return "ONNX: CUDA · 已加载";

        string? reason = _tagger.FallbackReason;
        if (!string.IsNullOrWhiteSpace(reason))
            return $"ONNX: CPU · 已加载（CUDA 不可用: {reason}）";
        return "ONNX: CPU · 已加载";
    }

    private void FlushOnnxLogsToStatus()
    {
        while (_onnxLogs.TryDequeue(out string? msg))
        {
            if (!string.IsNullOrWhiteSpace(msg))
                StatusText = msg;
        }
    }

    private void ReloadCurrentTags()
    {
        CurrentTags.Clear();
        SelectedTag = null;
        if (SelectedImage is null) return;
        foreach (var t in SelectedImage.Data.Tags.Items)
        {
            CurrentTags.Add(new TagRow
            {
                Tag = t.Tag,
                Weight = t.Weight,
                Chinese = _zhLookup.GetChinese(t.Tag),
            });
        }
    }

    private void ApplyCurrentTagsToModel()
    {
        if (SelectedImage is null) return;
        var tags = CurrentTags
            .Where(t => !string.IsNullOrWhiteSpace(t.Tag))
            .Select(t => (Tag: t.Tag.Trim(), Weight: t.Weight <= 0 ? 1f : t.Weight))
            .ToList();
        SelectedImage.Data.Tags.SetTags(tags);
    }

    private void RebuildGlobalTags()
    {
        GlobalTags.Clear();
        foreach (var item in TagStatistics.Build(_dataset.DataSet.Values, _zhLookup))
        {
            GlobalTags.Add(new TagCountRow
            {
                Tag = item.Tag,
                Count = item.Count,
                Chinese = item.Chinese,
            });
        }
    }

    private static Window? GetMainWindow()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}

public partial class ImageListItem : ObservableObject
{
    public ImageListItem(DatasetManager.DataItem data) => Data = data;
    public DatasetManager.DataItem Data { get; }
    public string Name => Data.Name;
    public string Path => Data.ImageFilePath;

    [ObservableProperty] private Bitmap? thumbnail;
    [ObservableProperty] private bool showFullPath;

    public override string ToString() => Name;
}

public partial class TagRow : ObservableObject
{
    [ObservableProperty] private string tag = string.Empty;
    [ObservableProperty] private string chinese = string.Empty;
    [ObservableProperty] private float weight = 1f;
}

public partial class TagCountRow : ObservableObject
{
    [ObservableProperty] private string tag = string.Empty;
    [ObservableProperty] private string chinese = string.Empty;
    [ObservableProperty] private int count;
}
