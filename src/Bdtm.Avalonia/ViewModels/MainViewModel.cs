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
    [ObservableProperty] private TagWriteMode writeMode = TagWriteMode.AppendNew;
    [ObservableProperty] private Bitmap? previewImage;
    [ObservableProperty] private bool showPaths;
    [ObservableProperty] private bool hasNoImages = true;
    [ObservableProperty] private string currentTagFilter = string.Empty;
    [ObservableProperty] private string globalTagFilter = string.Empty;
    [ObservableProperty] private double batchProgress;
    [ObservableProperty] private string batchStatus = string.Empty;
    [ObservableProperty] private bool isBatchRunning;
    private CancellationTokenSource? _batchCts;

    partial void OnSelectedImageChanged(ImageListItem? value)
    {
        ReloadCurrentTags();
        _ = UpdatePreviewAsync(value);
    }

    partial void OnOnnxModelRepoChanged(string value) => RefreshOnnxStatus();

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

            EnsureTagger();
            if (_tagger is null)
            {
                StatusText = "ONNX 服务未初始化。";
                return;
            }

            if (!_tagger.IsModelReady(OnnxModelRepo))
            {
                string expected = Wd14OnnxTaggerService.GetLocalPath(_modelsRoot, OnnxModelRepo, Wd14OnnxTaggerService.ModelFileName);
                StatusText = $"模型文件未找到。期望: {expected}";
                return;
            }

            if (!_tagger.IsLoaded || !string.Equals(_tagger.LoadedRepo, OnnxModelRepo, StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "正在加载 ONNX 会话…";
                await Task.Run(() => _tagger.LoadModel(OnnxModelRepo), ct);
                FlushOnnxLogsToStatus();
            }

            ProviderText = FormatProviderText(sessionLoaded: true);
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
                    var result = await Task.Run(
                        () => _tagger.TagImage(imageItem.Path, GeneralThreshold, CharacterThreshold),
                        ct);
                    await RunOnUiAsync(() =>
                    {
                        TagWriteService.ApplyTags(imageItem.Data, result.Tags, WriteMode, sortByConfidence: true);
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
