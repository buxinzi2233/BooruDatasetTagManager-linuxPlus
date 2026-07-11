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
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _onnxLogs = new();
    private AppSettings _settings;
    private Wd14OnnxTaggerService? _tagger;
    private string _modelsRoot = string.Empty;

    public MainViewModel()
    {
        _appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _settings = AppSettings.Load(_appDir);
        Images = new ObservableCollection<ImageListItem>();
        CurrentTags = new ObservableCollection<TagRow>();
        AllTags = new ObservableCollection<string>();

        if (!string.IsNullOrWhiteSpace(_settings.Wd14Tagger.SelectedModelRepo))
            OnnxModelRepo = _settings.Wd14Tagger.SelectedModelRepo;
        else if (!string.IsNullOrWhiteSpace(_settings.OnnxTaggerLastModelId))
            OnnxModelRepo = _settings.OnnxTaggerLastModelId;

        GeneralThreshold = _settings.Wd14Tagger.Threshold;
        CharacterThreshold = _settings.Wd14Tagger.CharacterThreshold;

        _modelsRoot = ResolveModelsRoot();
        RefreshOnnxStatus(prefix: "启动");
        StatusText = $"模型目录: {_modelsRoot}";
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

    partial void OnSelectedImageChanged(ImageListItem? value) => ReloadCurrentTags();

    partial void OnOnnxModelRepoChanged(string value) => RefreshOnnxStatus();

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
                Images.Add(new ImageListItem(item));

            RebuildAllTags();
            if (Images.Count > 0)
                SelectedImage = Images[0];

            StatusText = $"已加载 {Images.Count} 项 · {path}";
            RefreshOnnxStatus();
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
        var target = row ?? SelectedTag;
        if (SelectedImage is null || target is null) return;
        SelectedImage.Data.Tags.Remove(target.Tag);
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
                StatusText = $"模型文件未找到。期望: {expected} （以及同目录 selected_tags.csv）";
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
            RebuildAllTags();
            StatusText = $"打标完成 · {result.Tags.Count} tags · {result.ElapsedMilliseconds:F0} ms · {result.Provider}";
            ProviderText = $"ONNX: {result.Provider} · 已加载";
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
    private void RefreshOnnxStatusCommand()
    {
        _modelsRoot = ResolveModelsRoot();
        // Force tagger recreate if models root changed.
        if (_tagger is not null)
        {
            _tagger.Dispose();
            _tagger = null;
        }
        RefreshOnnxStatus(prefix: "刷新");
        StatusText = $"模型目录: {_modelsRoot}";
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
        // 1) Env override
        string? env = Environment.GetEnvironmentVariable("BDTM_MODELS_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        // 2) Settings
        if (!string.IsNullOrWhiteSpace(_settings.ModelsPath))
        {
            string configured = _settings.ModelsPath;
            if (!Path.IsPathRooted(configured))
                configured = Path.GetFullPath(Path.Combine(_appDir, configured));
            if (Directory.Exists(configured))
                return configured;
        }

        // 3) Next to app binary
        string besideApp = Path.Combine(_appDir, "Models");
        if (IsRepoPresent(besideApp, OnnxModelRepo) || Directory.Exists(besideApp))
            return besideApp;

        // 4) Known local caches on this machine (no download)
        string[] candidates =
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/toolbox/datasets/Tool/sd-image-sorter/data/models/wd14-tagger"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Projects/toolbox/model-train/AnimaLoraStudio/models/wd14"),
        };

        foreach (string c in candidates)
        {
            if (!Directory.Exists(c))
                continue;

            // layout A: .../wd14-tagger/wd-eva02-large-tagger-v3/{model.onnx}
            // map into a virtual SmilingWolf layout if needed via symlink-like resolve below
            if (IsRepoPresent(c, OnnxModelRepo))
                return c;

            // layout B: flat model folder names without org
            string flat = Path.Combine(c, OnnxModelRepo.Split('/').Last());
            if (File.Exists(Path.Combine(flat, "model.onnx")) && File.Exists(Path.Combine(flat, "selected_tags.csv")))
            {
                // Use parent and special-case GetLocalPath? Better: create adapter root with org/repo via temp? 
                // Simpler: if repo contains '/', also check parent/last-segment and return a synthetic root.
                // We'll handle flat layout in EnsureTagger by preferring an effective root that already has org/repo
                // or by rewriting Onnx path resolution here:
                // Create besideApp symlink tree lazily.
                TryLinkFlatModelInto(besideApp, OnnxModelRepo, flat);
                if (IsRepoPresent(besideApp, OnnxModelRepo))
                    return besideApp;
            }

            // layout C: SmilingWolf_wd-eva02-large-tagger-v3
            string underscored = OnnxModelRepo.Replace('/', '_');
            string underPath = Path.Combine(c, underscored);
            if (File.Exists(Path.Combine(underPath, "model.onnx")))
            {
                TryLinkFlatModelInto(besideApp, OnnxModelRepo, underPath);
                if (IsRepoPresent(besideApp, OnnxModelRepo))
                    return besideApp;
            }
        }

        // default: create empty Models dir next to app for user to fill
        Directory.CreateDirectory(besideApp);
        return besideApp;
    }

    private static void TryLinkFlatModelInto(string modelsRoot, string repo, string sourceDir)
    {
        try
        {
            string dest = Wd14OnnxTaggerService.GetLocalPath(modelsRoot, repo, ".");
            // GetLocalPath with "." ends with "/." — normalize to directory
            dest = Path.GetFullPath(Path.Combine(modelsRoot, repo.Replace('/', Path.DirectorySeparatorChar)));
            if (Directory.Exists(dest) || File.Exists(dest))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (Directory.Exists(sourceDir))
                Directory.CreateSymbolicLink(dest, sourceDir);
        }
        catch
        {
            // ignore link failures (permissions); user can copy manually
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
                : "ONNX: 模型文件就绪 · 会话未加载")
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
            return "ONNX: 模型文件就绪 · 会话未加载";
        return $"ONNX: {_tagger.ActiveProvider} · 已加载";
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
            CurrentTags.Add(new TagRow { Tag = t.Tag, Weight = t.Weight });
    }

    private void ApplyCurrentTagsToModel()
    {
        if (SelectedImage is null) return;
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
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}

public sealed class ImageListItem
{
    public ImageListItem(DatasetManager.DataItem data) => Data = data;
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
