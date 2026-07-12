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
    private PixAiOnnxTaggerService? _pixAiTagger;
    // 0 = WD14, 1 = PixAI

    private string _modelsRoot = string.Empty;
    private CancellationTokenSource? _previewCts;
    private int _thumbGen;

    public MainViewModel()
    {
        _appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        AppPaths.EnsureCreated();
        _settings = AppSettings.LoadUserSettings();
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
        StatusText = $"配置: {AppPaths.SettingsFilePath} · Models: {_modelsRoot} · 中文词表: {_zhLookup.Count}";
    }

    public ObservableCollection<ImageListItem> Images { get; }
    public ObservableCollection<TagRow> CurrentTags { get; }
    public ObservableCollection<TagCountRow> GlobalTags { get; }
    public ObservableCollection<TagRow> FilteredCurrentTags { get; } = new();
    public ObservableCollection<TagCountRow> FilteredGlobalTags { get; } = new();
    public ObservableCollection<ImageListItem> SelectedImages { get; } = new();

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
    [ObservableProperty] private int onnxEngineIndex; // 0=WD14, 1=PixAI
    [ObservableProperty] private string onnxEngineLabel = "WD14";
    [ObservableProperty] private TagWriteMode writeMode = TagWriteMode.AppendNew;
    [ObservableProperty] private Bitmap? previewImage;
    [ObservableProperty] private bool showPaths;
    [ObservableProperty] private bool hasNoImages = true;
    [ObservableProperty] private string currentTagFilter = string.Empty;
    [ObservableProperty] private string globalTagFilter = string.Empty;
    [ObservableProperty] private double batchProgress;
    [ObservableProperty] private string batchStatus = string.Empty;
    [ObservableProperty] private bool isBatchRunning;
    [ObservableProperty] private string downloadStatus = string.Empty;
    [ObservableProperty] private double downloadProgress;
    [ObservableProperty] private bool isDownloading;
    /// <summary>When true, single-image ONNX shows preview dialog before write.</summary>
    [ObservableProperty] private bool confirmOnnxBeforeWrite = true;
    [ObservableProperty] private string downloadSourceName = "HfMirror"; // or HuggingFace
    [ObservableProperty] private int downloadSourceIndex; // 0=HfMirror, 1=HuggingFace
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _batchCts;

    partial void OnSelectedImageChanged(ImageListItem? value)
    {
        ReloadCurrentTags();
        _ = UpdatePreviewAsync(value);
    }

    partial void OnOnnxModelRepoChanged(string value) => RefreshOnnxStatus();

    partial void OnOnnxEngineIndexChanged(int value)
    {
        OnnxEngineLabel = value == 1 ? "PixAI" : "WD14";
        if (value == 1)
            OnnxModelRepo = PixAiOnnxTaggerService.ModelRepo;
        else if (string.IsNullOrWhiteSpace(OnnxModelRepo) || OnnxModelRepo.Contains("pixai", StringComparison.OrdinalIgnoreCase))
            OnnxModelRepo = "SmilingWolf/wd-eva02-large-tagger-v3";

        // Drop loaded sessions when engine switches.
        _tagger?.Dispose();
        _tagger = null;
        _pixAiTagger?.Dispose();
        _pixAiTagger = null;
        RefreshOnnxStatus();
    }

    partial void OnShowPathsChanged(bool value)
    {
        // Force each row to refresh; ListBox virtualization can keep stale visuals.
        foreach (var item in Images)
            item.ShowFullPath = value;
        StatusText = value ? "已开启：列表显示完整路径" : "已关闭：列表隐藏完整路径";
    }

    partial void OnCurrentTagFilterChanged(string value) => ApplyCurrentTagFilter();

    partial void OnGlobalTagFilterChanged(string value) => ApplyGlobalTagFilter();

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
                SelectedImages.Clear();
                HasNoImages = true;
                CurrentTags.Clear();
                GlobalTags.Clear();
                FilteredCurrentTags.Clear();
                FilteredGlobalTags.Clear();
                CurrentTagFilter = string.Empty;
                GlobalTagFilter = string.Empty;
                SelectedImage = null;
                SelectedTag = null;
                SelectedGlobalTag = null;
                BatchProgress = 0;
                BatchStatus = string.Empty;
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
                HasNoImages = Images.Count == 0;
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

            if (!IsCurrentOnnxModelReady())
            {
                StatusText = CurrentModelMissingHint();
                RefreshOnnxStatus();
                return;
            }

            await EnsureOnnxLoadedAsync();
            var imageItem = SelectedImage;
            var result = await TagWithCurrentEngineAsync(imageItem.Data.ImageFilePath);

            IReadOnlyList<TagPrediction> tagsToApply = result.Tags;
            if (ConfirmOnnxBeforeWrite)
            {
                // ShowDialog MUST run on UI thread; await Task.Run() may resume on thread-pool.
                IsBusy = false;
                var previewDecision = await PromptOnnxPreviewAsync(
                    imageItem.Name,
                    result.Tags,
                    result.ElapsedMilliseconds,
                    result.Provider);
                if (!previewDecision.Apply)
                {
                    StatusText = previewDecision.StatusMessage
                        ?? ("已取消写入 · 推理 " + result.Tags.Count + " tags · " + result.ElapsedMilliseconds.ToString("F0") + " ms · " + result.Provider);
                    ProviderText = FormatProviderText(sessionLoaded: true);
                    return;
                }
                tagsToApply = previewDecision.Tags;
                IsBusy = true;
            }

            TagWriteService.ApplyTags(imageItem.Data, tagsToApply, WriteMode, sortByConfidence: true);
            ReloadCurrentTags();
            RebuildGlobalTags();
            StatusText = $"打标完成 · 写入 {tagsToApply.Count}/{result.Tags.Count} tags · {result.ElapsedMilliseconds:F0} ms · {result.Provider}";
            ProviderText = IsPixAiEngine
                ? FormatProviderTextFor(_pixAiTagger!.ActiveProvider, _pixAiTagger.FallbackReason, true)
                : FormatProviderText(sessionLoaded: true);
            string? fb = IsPixAiEngine ? _pixAiTagger?.FallbackReason : _tagger?.FallbackReason;
            if (result.Provider == OnnxExecutionProvider.Cpu && !string.IsNullOrWhiteSpace(fb))
                StatusText += " | CUDA 回退: " + fb;
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
        if (_pixAiTagger is not null)
        {
            _pixAiTagger.Dispose();
            _pixAiTagger = null;
        }
        RefreshOnnxStatus(prefix: "刷新");
        StatusText = $"Models: {_modelsRoot} · 配置: {AppPaths.SettingsFilePath} · 中文词表: {_zhLookup.Count}";
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
        if (!string.IsNullOrWhiteSpace(env))
        {
            Directory.CreateDirectory(env);
            return Path.GetFullPath(env);
        }

        if (!string.IsNullOrWhiteSpace(_settings.ModelsPath))
        {
            string configured = _settings.ModelsPath;
            if (!Path.IsPathRooted(configured))
                configured = Path.GetFullPath(Path.Combine(_appDir, configured));
            Directory.CreateDirectory(configured);
            return configured;
        }

        string userModels = AppPaths.DefaultModelsDir;
        Directory.CreateDirectory(userModels);
        if (IsRepoPresent(userModels, OnnxModelRepo))
            return userModels;

        string besideApp = Path.Combine(_appDir, "Models");
        if (IsRepoPresent(besideApp, OnnxModelRepo))
            return besideApp;

        // Auto-link known local caches into user models for convenience.
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/toolbox/datasets/Tool/sd-image-sorter/data/models/wd14-tagger"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/toolbox/model-train/AnimaLoraStudio/models/wd14"),
            besideApp,
        };

        foreach (string c in candidates)
        {
            if (!Directory.Exists(c)) continue;
            if (IsRepoPresent(c, OnnxModelRepo))
            {
                // If this is already userModels-compatible tree, use it; else link into userModels.
                if (string.Equals(Path.GetFullPath(c), Path.GetFullPath(userModels), StringComparison.Ordinal))
                    return userModels;
                string flat = Path.Combine(c, OnnxModelRepo.Split('/').Last());
                string under = Path.Combine(c, OnnxModelRepo.Replace('/', '_'));
                if (IsRepoPresent(c, OnnxModelRepo))
                {
                    // c itself may be Models root with org/repo
                    return c;
                }
                if (Directory.Exists(flat))
                    TryLinkFlatModelInto(userModels, OnnxModelRepo, flat);
                else if (Directory.Exists(under))
                    TryLinkFlatModelInto(userModels, OnnxModelRepo, under);
                if (IsRepoPresent(userModels, OnnxModelRepo))
                    return userModels;
            }

            string flat2 = Path.Combine(c, OnnxModelRepo.Split('/').Last());
            if (File.Exists(Path.Combine(flat2, "model.onnx")))
            {
                TryLinkFlatModelInto(userModels, OnnxModelRepo, flat2);
                if (IsRepoPresent(userModels, OnnxModelRepo))
                    return userModels;
            }

            string under2 = Path.Combine(c, OnnxModelRepo.Replace('/', '_'));
            if (File.Exists(Path.Combine(under2, "model.onnx")))
            {
                TryLinkFlatModelInto(userModels, OnnxModelRepo, under2);
                if (IsRepoPresent(userModels, OnnxModelRepo))
                    return userModels;
            }
        }

        return userModels;
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
        bool filesReady = IsCurrentOnnxModelReady();
        bool sessionLoaded = IsPixAiEngine
            ? (_pixAiTagger?.IsLoaded == true)
            : (_tagger?.IsLoaded == true && string.Equals(_tagger.LoadedRepo, OnnxModelRepo, StringComparison.OrdinalIgnoreCase));

        if (filesReady)
        {
            if (sessionLoaded)
            {
                ProviderText = IsPixAiEngine
                    ? FormatProviderTextFor(_pixAiTagger!.ActiveProvider, _pixAiTagger.FallbackReason, true)
                    : FormatProviderText(sessionLoaded: true);
            }
            else
            {
                ProviderText = IsPixAiEngine
                    ? "ONNX(PixAI): 模型已就绪（点打标后加载）"
                    : "ONNX(WD14): 模型已就绪（点「ONNX 当前图」后加载到 GPU/CPU）";
            }
        }
        else
        {
            ProviderText = IsPixAiEngine ? "ONNX(PixAI): 模型文件缺失" : "ONNX(WD14): 模型文件缺失";
        }

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
        ApplyCurrentTagFilter();
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
        ApplyGlobalTagFilter();
    }


    private void ApplyCurrentTagFilter()
    {
        FilteredCurrentTags.Clear();
        string q = (CurrentTagFilter ?? string.Empty).Trim();
        foreach (var row in CurrentTags)
        {
            if (string.IsNullOrEmpty(q)
                || row.Tag.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(row.Chinese) && row.Chinese.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredCurrentTags.Add(row);
            }
        }
    }

    private void ApplyGlobalTagFilter()
    {
        FilteredGlobalTags.Clear();
        string q = (GlobalTagFilter ?? string.Empty).Trim();
        foreach (var row in GlobalTags)
        {
            if (string.IsNullOrEmpty(q)
                || row.Tag.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(row.Chinese) && row.Chinese.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredGlobalTags.Add(row);
            }
        }
    }

    [RelayCommand]
    private void MoveTagUp()
    {
        if (SelectedImage is null || SelectedTag is null) return;
        ApplyCurrentTagsToModel();
        int idx = SelectedImage.Data.Tags.IndexOf(SelectedTag.Tag);
        if (idx < 0) return;
        if (!SelectedImage.Data.Tags.MoveUp(idx)) return;
        string keep = SelectedTag.Tag;
        ReloadCurrentTags();
        SelectedTag = FilteredCurrentTags.FirstOrDefault(r => r.Tag == keep)
            ?? CurrentTags.FirstOrDefault(r => r.Tag == keep);
    }

    [RelayCommand]
    private void MoveTagDown()
    {
        if (SelectedImage is null || SelectedTag is null) return;
        ApplyCurrentTagsToModel();
        int idx = SelectedImage.Data.Tags.IndexOf(SelectedTag.Tag);
        if (idx < 0) return;
        if (!SelectedImage.Data.Tags.MoveDown(idx)) return;
        string keep = SelectedTag.Tag;
        ReloadCurrentTags();
        SelectedTag = FilteredCurrentTags.FirstOrDefault(r => r.Tag == keep)
            ?? CurrentTags.FirstOrDefault(r => r.Tag == keep);
    }

    [RelayCommand]
    private void RemoveSelectedTagRows()
    {
        // Single-selection MVP: remove SelectedTag; multi-select can bind same command later.
        RemoveSelectedTag(SelectedTag);
    }

    [RelayCommand]
    private async Task RunOnnxOnSelectedAsync()
    {
        var targets = SelectedImages.Count > 0
            ? SelectedImages.ToList()
            : (SelectedImage is null ? new List<ImageListItem>() : new List<ImageListItem> { SelectedImage });
        if (targets.Count == 0)
        {
            StatusText = "请先选择一张或多张图片。";
            return;
        }
        await RunOnnxBatchAsync(targets, "选中");
    }

    public void SetSelectedImages(IEnumerable<ImageListItem> items)
    {
        SelectedImages.Clear();
        foreach (var i in items)
            SelectedImages.Add(i);
        if (SelectedImages.Count > 0 && (SelectedImage is null || !SelectedImages.Contains(SelectedImage)))
            SelectedImage = SelectedImages[^1];
    }

    [RelayCommand]
    private async Task RunOnnxOnAllAsync()
    {
        if (Images.Count == 0)
        {
            StatusText = "请先打开数据集。";
            return;
        }
        await RunOnnxBatchAsync(Images.ToList(), "全部");
    }

    [RelayCommand]
    private void CancelBatch()
    {
        _batchCts?.Cancel();
    }

    private async Task RunOnnxBatchAsync(IReadOnlyList<ImageListItem> targets, string modeLabel)
    {
        if (IsBusy || IsBatchRunning) return;
        if (targets.Count == 0)
        {
            StatusText = "没有可打标的图片。";
            return;
        }

        // If only one target and mode is 选中, reuse single path semantics but with progress.
        _batchCts?.Cancel();
        _batchCts = new CancellationTokenSource();
        var ct = _batchCts.Token;

        try
        {
            IsBatchRunning = true;
            IsBusy = true;
            BatchProgress = 0;
            BatchStatus = $"批量 ONNX（{modeLabel}）0/{targets.Count}";
            StatusText = BatchStatus;

            if (!IsCurrentOnnxModelReady())
            {
                StatusText = CurrentModelMissingHint();
                return;
            }

            await EnsureOnnxLoadedAsync(ct);
            int done = 0;
            int okCount = 0;
            int failCount = 0;
            var swAll = System.Diagnostics.Stopwatch.StartNew();

            foreach (var imageItem in targets)
            {
                ct.ThrowIfCancellationRequested();
                if (!ImageThumbnailLoader.IsRasterImage(imageItem.Path))
                {
                    done++;
                    continue;
                }

                try
                {
                    var result = await TagWithCurrentEngineAsync(imageItem.Path, ct);

                    IReadOnlyList<TagPrediction> tagsToApply = result.Tags;
                    if (ConfirmOnnxBeforeWrite && targets.Count == 1)
                    {
                        await RunOnUiAsync(() => { IsBusy = false; IsBatchRunning = false; });
                        var previewDecision = await PromptOnnxPreviewAsync(
                            imageItem.Name,
                            result.Tags,
                            result.ElapsedMilliseconds,
                            result.Provider);
                        if (!previewDecision.Apply)
                        {
                            StatusText = previewDecision.StatusMessage ?? "已取消写入（预览）。";
                            return;
                        }
                        tagsToApply = previewDecision.Tags;
                        await RunOnUiAsync(() => { IsBusy = true; IsBatchRunning = true; });
                    }

                    await RunOnUiAsync(() =>
                    {
                        TagWriteService.ApplyTags(imageItem.Data, tagsToApply, WriteMode, sortByConfidence: true);
                        if (ReferenceEquals(SelectedImage, imageItem))
                            ReloadCurrentTags();
                    });
                    okCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failCount++;
                    System.Diagnostics.Debug.WriteLine("batch tag fail: " + imageItem.Path + " " + ex.Message);
                }

                done++;
                double p = 100.0 * done / targets.Count;
                await RunOnUiAsync(() =>
                {
                    BatchProgress = p;
                    BatchStatus = $"批量 ONNX（{modeLabel}）{done}/{targets.Count} · 成功 {okCount} · 失败 {failCount}";
                    StatusText = BatchStatus;
                });
            }

            swAll.Stop();
            await RunOnUiAsync(() =>
            {
                RebuildGlobalTags();
                StatusText = $"批量完成（{modeLabel}）· 成功 {okCount} · 失败 {failCount} · {swAll.ElapsedMilliseconds} ms · {_tagger.ActiveProvider}";
                ProviderText = FormatProviderText(sessionLoaded: true);
                BatchProgress = 100;
            });
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() => StatusText = "批量 ONNX 已取消。");
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() => StatusText = "批量 ONNX 失败: " + ex.Message);
            FlushOnnxLogsToStatus();
            RefreshOnnxStatus();
        }
        finally
        {
            await RunOnUiAsync(() =>
            {
                IsBatchRunning = false;
                IsBusy = false;
            });
        }
    }


    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var window = GetMainWindow();
        if (window is null) return;
        var vm = new SettingsViewModel(_settings, onSaved: ApplySettingsLive);
        var dlg = new Views.SettingsWindow { DataContext = vm };
        await dlg.ShowDialog(window);
        ApplySettingsLive();
    }

    [RelayCommand]
    private async Task OpenWikiAsync()
    {
        string? tag = SelectedTag?.Tag ?? SelectedGlobalTag?.Tag;
        if (string.IsNullOrWhiteSpace(tag))
        {
            StatusText = "请先在当前标签或全部标签中选中一个标签。";
            return;
        }

        var window = GetMainWindow();
        if (window is null) return;
        var vm = new WikiViewModel(tag);
        var dlg = new Views.WikiWindow { DataContext = vm };
        await dlg.ShowDialog(window);
    }

    private void ApplySettingsLive()
    {
        if (!string.IsNullOrWhiteSpace(_settings.Wd14Tagger.SelectedModelRepo))
            OnnxModelRepo = _settings.Wd14Tagger.SelectedModelRepo;
        GeneralThreshold = _settings.Wd14Tagger.Threshold;
        CharacterThreshold = _settings.Wd14Tagger.CharacterThreshold;
        _modelsRoot = ResolveModelsRoot();
        if (_tagger is not null)
        {
            _tagger.Dispose();
            _tagger = null;
        }
        RefreshOnnxStatus();
        StatusText = $"设置已应用 · Models: {_modelsRoot}";
    }


    [RelayCommand]
    private async Task OpenVideoToolsAsync()
    {
        var window = GetMainWindow();
        if (window is null) return;

        string? initial = null;
        if (SelectedImage is not null && VideoProcessingService.IsVideoFile(SelectedImage.Path))
            initial = SelectedImage.Path;
        else if (SelectedImages.Count > 0)
        {
            initial = SelectedImages.Select(i => i.Path).FirstOrDefault(VideoProcessingService.IsVideoFile);
        }

        string? pendingLoad = null;
        var vm = new VideoToolsViewModel(
            CreateFfmpegLocator,
            initial,
            loadDataset: path => pendingLoad = path);
        var dlg = new Views.VideoToolsWindow { DataContext = vm };
        await dlg.ShowDialog(window);

        if (!string.IsNullOrWhiteSpace(pendingLoad) && Directory.Exists(pendingLoad))
        {
            StatusText = "正在加载抽帧输出为数据集…";
            await LoadDatasetAsync(pendingLoad);
            return;
        }

        if (!string.IsNullOrWhiteSpace(vm.LastOutputDirectory)
            && !string.IsNullOrWhiteSpace(DatasetPath)
            && Directory.Exists(DatasetPath)
            && Path.GetFullPath(vm.LastOutputDirectory!).StartsWith(Path.GetFullPath(DatasetPath), StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "抽帧输出在当前数据集目录下。可点视频工具内「加载为数据集」或重新打开文件夹: " + vm.LastOutputDirectory;
        }
        else if (!string.IsNullOrWhiteSpace(vm.LastOutputDirectory))
        {
            StatusText = "抽帧完成。可在视频工具中点「加载为数据集」: " + vm.LastOutputDirectory;
        }
    }

    private FfmpegLocator CreateFfmpegLocator()
    {
        return new FfmpegLocator(_appDir, _settings.FfmpegPath ?? string.Empty);
    }


    [RelayCommand]
    private async Task DownloadOnnxModelAsync()
    {
        if (IsDownloading || IsBusy) return;
        if (string.IsNullOrWhiteSpace(OnnxModelRepo))
        {
            StatusText = "请先填写模型 repo（例如 SmilingWolf/wd-eva02-large-tagger-v3）。";
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;
        string modelsRoot = ResolveModelsRoot();
        Directory.CreateDirectory(modelsRoot);

        var source = DownloadSourceIndex == 1
            ? HuggingFaceDownloadSource.HuggingFace
            : HuggingFaceDownloadSource.HfMirror;
        DownloadSourceName = source == HuggingFaceDownloadSource.HuggingFace ? "HuggingFace" : "HfMirror";

        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatus = "准备下载…";
            StatusText = $"下载模型 {OnnxModelRepo}（{source}）→ {modelsRoot}";

            var dl = new HuggingFaceModelDownloader(modelsRoot);
            bool ready = IsPixAiEngine
                ? dl.AreFilesCached(PixAiOnnxTaggerService.ModelRepo, PixAiOnnxTaggerService.RequiredFiles)
                : dl.IsModelReady(OnnxModelRepo);
            if (ready)
            {
                DownloadProgress = 100;
                DownloadStatus = "模型已在本地，无需下载。";
                StatusText = DownloadStatus + " " + dl.GetLocalDirectory(IsPixAiEngine ? PixAiOnnxTaggerService.ModelRepo : OnnxModelRepo);
                RefreshOnnxStatus();
                return;
            }

            var progress = new Progress<(string file, long downloaded, long? total)>(p =>
            {
                string msg;
                if (p.total is long tot && tot > 0)
                {
                    double pct = 100.0 * p.downloaded / tot;
                    DownloadProgress = Math.Min(100, pct);
                    msg = $"{p.file}: {p.downloaded / 1048576.0:0.0}/{tot / 1048576.0:0.0} MB ({pct:0.0}%)";
                }
                else
                {
                    msg = $"{p.file}: {p.downloaded / 1048576.0:0.0} MB";
                }
                DownloadStatus = msg;
                StatusText = msg;
            });

            if (IsPixAiEngine)
            {
                OnnxModelRepo = PixAiOnnxTaggerService.ModelRepo;
                await dl.DownloadFilesAsync(source, OnnxModelRepo, PixAiOnnxTaggerService.RequiredFiles, progress, ct);
            }
            else
            {
                await dl.DownloadModelAsync(source, OnnxModelRepo, progress, ct);
            }
            DownloadProgress = 100;
            DownloadStatus = "下载完成";
            StatusText = $"模型就绪: {dl.GetLocalDirectory(OnnxModelRepo)}";
            _settings.Wd14Tagger.SelectedModelRepo = OnnxModelRepo;
            _settings.OnnxTaggerLastModelId = OnnxModelRepo;
            _settings.ModelsPath = modelsRoot;
            try { _settings.Save(); } catch { /* ignore */ }
            RefreshOnnxStatus();
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "下载已取消";
            StatusText = DownloadStatus;
        }
        catch (Exception ex)
        {
            DownloadStatus = "下载失败: " + ex.Message;
            StatusText = DownloadStatus;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }


    private sealed class OnnxPreviewDecision
    {
        public bool Apply { get; init; }
        public IReadOnlyList<TagPrediction> Tags { get; init; } = Array.Empty<TagPrediction>();
        public string? StatusMessage { get; init; }
    }

    private Task<OnnxPreviewDecision> PromptOnnxPreviewAsync(
        string imageName,
        IReadOnlyList<TagPrediction> predictions,
        double elapsedMs,
        OnnxExecutionProvider provider)
    {
        async Task<OnnxPreviewDecision> ShowCoreAsync()
        {
            var previewVm = new OnnxPreviewViewModel(
                imageName,
                predictions,
                _zhLookup,
                WriteMode,
                elapsedMs,
                provider);

            var window = GetMainWindow();
            if (window is null)
            {
                return new OnnxPreviewDecision
                {
                    Apply = false,
                    StatusMessage = "无法显示预览窗（主窗口为空）。未写入标签。",
                };
            }

            try
            {
                var dlg = new Views.OnnxPreviewWindow { DataContext = previewVm };
                var applied = await dlg.ShowDialog<bool?>(window);
                if (applied == true && previewVm.Confirmed)
                {
                    return new OnnxPreviewDecision
                    {
                        Apply = true,
                        Tags = previewVm.GetSelectedPredictions(),
                    };
                }

                return new OnnxPreviewDecision
                {
                    Apply = false,
                    StatusMessage = "已取消写入 · 推理 " + predictions.Count + " tags · " + elapsedMs.ToString("F0") + " ms · " + provider,
                };
            }
            catch (Exception ex)
            {
                return new OnnxPreviewDecision
                {
                    Apply = false,
                    StatusMessage = "预览窗打开失败: " + ex.Message + "（未写入）",
                };
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
            return ShowCoreAsync();

        // Avalonia 11: InvokeAsync(Func<Task<T>>) returns Task<T>
        return Dispatcher.UIThread.InvokeAsync(ShowCoreAsync);
    }


    private bool IsPixAiEngine => OnnxEngineIndex == 1;

    private bool IsCurrentOnnxModelReady()
    {
        if (IsPixAiEngine)
        {
            EnsurePixAiTagger();
            return _pixAiTagger!.IsModelReady();
        }
        return IsRepoPresent(_modelsRoot, OnnxModelRepo);
    }

    private string CurrentModelMissingHint()
    {
        if (IsPixAiEngine)
            return "PixAI 模型文件不完整。请下载: " + PixAiOnnxTaggerService.ModelRepo
                + "（需 model.onnx/selected_tags.csv/categories.json/preprocess.json/thresholds.csv）→ " + _modelsRoot;
        return "模型文件未找到。期望: " + Wd14OnnxTaggerService.GetLocalPath(_modelsRoot, OnnxModelRepo, Wd14OnnxTaggerService.ModelFileName);
    }

    private void EnsurePixAiTagger()
    {
        _modelsRoot = ResolveModelsRoot();
        if (_pixAiTagger is not null) return;
        var factory = new OnnxSessionFactory(msg =>
        {
            _onnxLogs.Enqueue(msg);
            System.Diagnostics.Debug.WriteLine(msg);
        });
        _pixAiTagger = new PixAiOnnxTaggerService(factory, _modelsRoot);
    }

    private async Task EnsureOnnxLoadedAsync(CancellationToken ct = default)
    {
        if (IsPixAiEngine)
        {
            EnsurePixAiTagger();
            if (!_pixAiTagger!.IsLoaded)
            {
                StatusText = "正在加载 PixAI ONNX 会话…";
                await Task.Run(() => _pixAiTagger.LoadModel(), ct);
                FlushOnnxLogsToStatus();
            }
            ProviderText = FormatProviderTextFor(_pixAiTagger.ActiveProvider, _pixAiTagger.FallbackReason, true);
            return;
        }

        EnsureTagger();
        if (_tagger is null)
            throw new InvalidOperationException("ONNX 服务未初始化。");
        if (!_tagger.IsLoaded || !string.Equals(_tagger.LoadedRepo, OnnxModelRepo, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "正在加载 WD14 ONNX 会话…";
            await Task.Run(() => _tagger.LoadModel(OnnxModelRepo), ct);
            FlushOnnxLogsToStatus();
        }
        ProviderText = FormatProviderText(sessionLoaded: true);
    }

    private async Task<OnnxTagResult> TagWithCurrentEngineAsync(string imagePath, CancellationToken ct = default)
    {
        if (IsPixAiEngine)
        {
            EnsurePixAiTagger();
            return await Task.Run(() => _pixAiTagger!.TagImage(imagePath, GeneralThreshold, CharacterThreshold), ct);
        }
        EnsureTagger();
        return await Task.Run(() => _tagger!.TagImage(imagePath, GeneralThreshold, CharacterThreshold), ct);
    }

    private string FormatProviderTextFor(OnnxExecutionProvider provider, string? fallback, bool sessionLoaded)
    {
        string engine = IsPixAiEngine ? "PixAI" : "WD14";
        if (!sessionLoaded)
            return $"ONNX({engine}): 模型已就绪（点打标后加载）";
        if (provider == OnnxExecutionProvider.Cuda)
            return $"ONNX({engine}): CUDA · 已加载";
        if (!string.IsNullOrWhiteSpace(fallback))
            return $"ONNX({engine}): CPU · 已加载（CUDA 不可用: {fallback}）";
        return $"ONNX({engine}): CPU · 已加载";
    }


    [RelayCommand]
    private async Task RunLlmOnCurrentAsync()
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
            StatusText = "LLM 视觉打标中…";
            if (string.IsNullOrWhiteSpace(_settings.Llm.Endpoint) || string.IsNullOrWhiteSpace(_settings.Llm.VisionModel))
            {
                StatusText = "请先在设置中配置 LLM Endpoint 与 Vision 模型。";
                return;
            }

            var imageItem = SelectedImage;
            LlmTagResult result;
            using (var tagger = new OpenAiVisionTagger(_settings.Llm))
            {
                result = await tagger.TagImageAsync(imageItem.Data.ImageFilePath);
            }

            if (!result.Success)
            {
                StatusText = "LLM 失败: " + (result.ErrorMessage ?? "unknown");
                return;
            }

            var predictions = result.Tags
                .Select(t => new TagPrediction { Tag = t.Tag, Confidence = t.Confidence })
                .ToList();

            IReadOnlyList<TagPrediction> tagsToApply = predictions;
            if (ConfirmOnnxBeforeWrite)
            {
                IsBusy = false;
                var previewDecision = await PromptOnnxPreviewAsync(
                    imageItem.Name + " (LLM)",
                    predictions,
                    result.ElapsedMilliseconds,
                    OnnxExecutionProvider.Cpu);
                if (!previewDecision.Apply)
                {
                    StatusText = previewDecision.StatusMessage ?? "已取消 LLM 写入。";
                    return;
                }
                tagsToApply = previewDecision.Tags;
                IsBusy = true;
            }

            TagWriteService.ApplyTags(imageItem.Data, tagsToApply, WriteMode, sortByConfidence: false);
            ReloadCurrentTags();
            RebuildGlobalTags();
            StatusText = $"LLM 完成 · 写入 {tagsToApply.Count} tags · {result.ElapsedMilliseconds:F0} ms";
        }
        catch (Exception ex)
        {
            StatusText = "LLM 失败: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool HasValidTag2NlSettings(LlmSettings llm)
    {
        if (string.IsNullOrWhiteSpace(llm.VisionModel)) return false;
        if (!Uri.TryCreate((llm.Endpoint ?? string.Empty).Trim(), UriKind.Absolute, out var endpoint))
            return false;
        return endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps;
    }

    private bool DatasetHasUnsavedChanges()
    {
        if (string.IsNullOrEmpty(_dataset.DatasetRoot)) return false;
        string sep = _settings.SeparatorOnSave;
        return _dataset.DataSet.Values.Any(i => i.IsModified(sep));
    }


    [RelayCommand]
    private async Task OpenCharacterTagAuditAsync()
    {
        if (IsBusy || IsBatchRunning) return;
        if (string.IsNullOrWhiteSpace(_dataset.DatasetRoot) || _dataset.DataSet.Count == 0)
        {
            StatusText = "请先打开数据集文件夹。";
            return;
        }

        var window = GetMainWindow();
        if (window is null) return;

        try
        {
            // Flush tag editor; auto-save dirty tags before audit (same as TAG2NL).
            ApplyCurrentTagsToModel();
            if (DatasetHasUnsavedChanges())
            {
                _dataset.SaveAll();
                StatusText = "已保存修改，打开角色标签审计…";
            }

            string auditModel = string.IsNullOrWhiteSpace(_settings.CharacterTagAuditModel)
                ? _settings.Llm.VisionModel
                : _settings.CharacterTagAuditModel;
            if (string.IsNullOrWhiteSpace(auditModel) || !HasValidLlmEndpoint(_settings.Llm))
            {
                StatusText = "请先在设置中配置有效的 LLM Endpoint，并填写审计模型或 Vision 模型。";
                await OpenSettingsAsync();
                return;
            }

            IsBusy = true;
            StatusText = "角色标签审计…";

            async Task<bool> ConfirmAsync(string title, string message)
            {
                var dlg = new Window
                {
                    Title = title,
                    Width = 460,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                };
                var ok = false;
                var panel = new DockPanel { Margin = new global::Avalonia.Thickness(16) };
                var buttons = new StackPanel
                {
                    Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                };
                DockPanel.SetDock(buttons, Dock.Bottom);
                var cancelBtn = new Button { Content = "取消", MinWidth = 80 };
                var okBtn = new Button { Content = "确定", MinWidth = 80 };
                cancelBtn.Click += (_, _) => { ok = false; dlg.Close(); };
                okBtn.Click += (_, _) => { ok = true; dlg.Close(); };
                buttons.Children.Add(cancelBtn);
                buttons.Children.Add(okBtn);
                panel.Children.Add(buttons);
                panel.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                });
                dlg.Content = panel;
                await dlg.ShowDialog(window);
                return ok;
            }

            void Alert(string message)
            {
                StatusText = message;
            }

            var wizardVm = new CharacterTagAuditWizardViewModel(
                _dataset,
                _settings,
                onApplied: () =>
                {
                    ReloadCurrentTags();
                    RebuildGlobalTags();
                },
                confirmAsync: ConfirmAsync,
                alert: Alert);

            var wizard = new Views.CharacterTagAuditWizardWindow { DataContext = wizardVm };
            var result = await wizard.ShowDialog<bool?>(window);
            if (result == true)
                StatusText = "角色标签审计已应用。";
            else if (!string.IsNullOrWhiteSpace(wizardVm.StatusMessage))
                StatusText = wizardVm.StatusMessage;
            else
                StatusText = "已关闭角色标签审计。";
        }
        catch (Exception ex)
        {
            StatusText = "角色标签审计失败: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool HasValidLlmEndpoint(LlmSettings llm)
    {
        if (!Uri.TryCreate((llm.Endpoint ?? string.Empty).Trim(), UriKind.Absolute, out var endpoint))
            return false;
        return endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps;
    }

    [RelayCommand]
    private async Task RunTag2NlAsync()
    {
        if (IsBusy || IsBatchRunning) return;
        if (string.IsNullOrWhiteSpace(_dataset.DatasetRoot))
        {
            StatusText = "请先打开数据集文件夹。";
            return;
        }

        var window = GetMainWindow();
        if (window is null) return;

        try
        {
            // Flush tag editor into model; auto-save dirty dataset before scanning (Linux MVP).
            ApplyCurrentTagsToModel();
            if (DatasetHasUnsavedChanges())
            {
                _dataset.SaveAll();
                StatusText = "已保存修改，开始 TAG2NL…";
            }

            if (!HasValidTag2NlSettings(_settings.Llm))
            {
                StatusText = "请先在设置中配置有效的 LLM Endpoint 与 Vision 模型。";
                await OpenSettingsAsync();
                return;
            }

            IsBusy = true;
            StatusText = "TAG2NL：扫描中…";
            CaptionScanResult scan;
            try
            {
                scan = await CaptionGenerationService.ScanDirectoryAsync(_dataset.DatasetRoot);
            }
            catch (Exception ex)
            {
                StatusText = "TAG2NL 扫描失败: " + ex.Message;
                return;
            }
            finally
            {
                IsBusy = false;
            }

            var confirmVm = new Tag2NlConfirmViewModel(scan);
            var confirm = new Views.Tag2NlConfirmWindow { DataContext = confirmVm };
            var ok = await confirm.ShowDialog<bool?>(window);
            if (ok != true)
            {
                StatusText = "已取消 TAG2NL。";
                return;
            }

            bool skipExisting = !confirmVm.ReprocessExisting;
            if (skipExisting && scan.Pending == 0)
            {
                StatusText = $"TAG2NL 完成 · 成功 0 · 跳过 {scan.Existing} · 失败 0 · 输出 {scan.OutputRoot}";
                return;
            }

            var progressVm = new Tag2NlProgressViewModel();
            var progressWin = new Views.Tag2NlProgressWindow { DataContext = progressVm };
            var progress = new Progress<CaptionGenerationProgress>(p =>
            {
                Dispatcher.UIThread.Post(() => progressVm.ApplyProgress(p));
            });

            IsBusy = true;
            progressWin.Show(window);

            CaptionGenerationResult result;
            try
            {
                using var client = new OpenAiVisionClient(_settings.Llm);
                var service = new CaptionGenerationService(async (req, ct) =>
                {
                    var completion = await client.CompleteAsync(new OpenAiVisionCompletionRequest
                    {
                        SystemPrompt = req.SystemPrompt,
                        UserPrompt = req.UserPrompt,
                        ImageData = req.ImageData,
                        ContentType = req.ContentType,
                    }, ct).ConfigureAwait(false);

                    if (!completion.Success)
                        return new CaptionModelResponse(string.Empty, completion.ErrorMessage ?? "LLM error");
                    return new CaptionModelResponse(completion.Text ?? string.Empty, string.Empty);
                });

                result = await service.ProcessAsync(
                    scan,
                    new CaptionGenerationOptions
                    {
                        SystemPrompt = string.IsNullOrWhiteSpace(_settings.Llm.Tag2NlSystemPrompt)
                            ? LlmDefaults.Tag2NlSystemPrompt
                            : _settings.Llm.Tag2NlSystemPrompt,
                        SkipExisting = skipExisting,
                        MaxConcurrency = _settings.Llm.Tag2NlConcurrency,
                    },
                    progress,
                    progressVm.Token).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                result = new CaptionGenerationResult { Failed = 1 };
                result.Errors.Add(ex.Message);
            }
            finally
            {
                progressVm.AllowClose = true;
                try { progressWin.Close(); } catch { /* ignore */ }
                IsBusy = false;
            }

            string prefix = result.Canceled ? "TAG2NL 已取消" : "TAG2NL 完成";
            string status =
                $"{prefix} · 成功 {result.Succeeded} · 跳过 {result.Skipped} · 失败 {result.Failed} · 输出 {scan.OutputRoot}";
            if (result.Errors.Count > 0)
                status += " | 错误: " + string.Join("; ", result.Errors.Take(5));

            await RunOnUiAsync(() => StatusText = status);
        }
        catch (Exception ex)
        {
            StatusText = "TAG2NL 失败: " + ex.Message;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CropCurrentImageAsync()
    {
        if (IsBusy) return;
        if (SelectedImage is null)
        {
            StatusText = "请先选择一张图片。";
            return;
        }

        string path = SelectedImage.Data.ImageFilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = "图片不存在。";
            return;
        }

        if (!ImageThumbnailLoader.IsRasterImage(path))
        {
            StatusText = "当前项不是可裁剪的光栅图片。";
            return;
        }

        var window = GetMainWindow();
        if (window is null) return;

        var vm = new CropImageViewModel(path);
        var dlg = new Views.CropImageWindow { DataContext = vm };
        var ok = await dlg.ShowDialog<bool?>(window);
        if (ok != true || vm.ExportedPaths.Count == 0)
        {
            StatusText = "已取消裁剪。";
            return;
        }

        var added = _dataset.AddImages(vm.ExportedPaths);
        bool showPaths = ShowPaths;
        foreach (string p in added)
        {
            if (_dataset.DataSet.TryGetValue(p, out var data))
                Images.Add(new ImageListItem(data) { ShowFullPath = showPaths });
        }

        HasNoImages = Images.Count == 0;
        StatusText = $"已导入裁剪图 {added.Count} 张 · 导出 {vm.ExportedPaths.Count} 张";
        _ = LoadThumbnailsAsync();
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
