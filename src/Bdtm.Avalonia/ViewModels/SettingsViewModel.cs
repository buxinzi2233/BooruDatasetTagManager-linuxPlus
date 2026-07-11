using System.IO;
using Bdtm.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bdtm.Avalonia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Action? _onSaved;

    public SettingsViewModel(AppSettings settings, Action? onSaved = null)
    {
        _settings = settings;
        _onSaved = onSaved;
        ReloadFromSettings();
    }

    [ObservableProperty] private string language = "zh-CN";
    [ObservableProperty] private string separatorOnLoad = ",";
    [ObservableProperty] private string separatorOnSave = ", ";
    [ObservableProperty] private string defaultTagsFileExtension = "txt";
    [ObservableProperty] private bool fixTagsOnSaveLoad = true;
    [ObservableProperty] private bool askSaveChanges = true;
    [ObservableProperty] private int previewSize = 130;
    [ObservableProperty] private string modelsPath = string.Empty;
    [ObservableProperty] private string ffmpegPath = string.Empty;
    [ObservableProperty] private string onnxModelRepo = string.Empty;
    [ObservableProperty] private double generalThreshold = 0.52;
    [ObservableProperty] private double characterThreshold = 0.85;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string configPathDisplay = string.Empty;
    [ObservableProperty] private string defaultModelsHint = string.Empty;

    public void ReloadFromSettings()
    {
        Language = _settings.Language;
        SeparatorOnLoad = _settings.SeparatorOnLoad;
        SeparatorOnSave = _settings.SeparatorOnSave;
        DefaultTagsFileExtension = _settings.DefaultTagsFileExtension;
        FixTagsOnSaveLoad = _settings.FixTagsOnSaveLoad;
        AskSaveChanges = _settings.AskSaveChanges;
        PreviewSize = _settings.PreviewSize;
        ModelsPath = string.IsNullOrWhiteSpace(_settings.ModelsPath)
            ? AppPaths.DefaultModelsDir
            : _settings.ModelsPath;
        FfmpegPath = _settings.FfmpegPath;
        OnnxModelRepo = string.IsNullOrWhiteSpace(_settings.Wd14Tagger.SelectedModelRepo)
            ? _settings.OnnxTaggerLastModelId
            : _settings.Wd14Tagger.SelectedModelRepo;
        GeneralThreshold = _settings.Wd14Tagger.Threshold;
        CharacterThreshold = _settings.Wd14Tagger.CharacterThreshold;
        ConfigPathDisplay = AppPaths.SettingsFilePath;
        DefaultModelsHint = AppPaths.DefaultModelsDir;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ResetModelsPath()
    {
        ModelsPath = AppPaths.DefaultModelsDir;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Language = Language;
        _settings.SeparatorOnLoad = SeparatorOnLoad;
        _settings.SeparatorOnSave = SeparatorOnSave;
        _settings.DefaultTagsFileExtension = DefaultTagsFileExtension;
        _settings.FixTagsOnSaveLoad = FixTagsOnSaveLoad;
        _settings.AskSaveChanges = AskSaveChanges;
        _settings.PreviewSize = PreviewSize <= 0 ? 130 : PreviewSize;
        _settings.ModelsPath = string.IsNullOrWhiteSpace(ModelsPath)
            ? AppPaths.DefaultModelsDir
            : ModelsPath.Trim();
        _settings.FfmpegPath = FfmpegPath?.Trim() ?? string.Empty;
        _settings.OnnxTaggerLastModelId = OnnxModelRepo?.Trim() ?? string.Empty;
        _settings.Wd14Tagger.SelectedModelRepo = OnnxModelRepo?.Trim() ?? string.Empty;
        _settings.Wd14Tagger.Threshold = GeneralThreshold;
        _settings.Wd14Tagger.CharacterThreshold = CharacterThreshold;
        try
        {
            Directory.CreateDirectory(_settings.ModelsPath);
            _settings.Save();
            StatusMessage = "已保存到 " + AppPaths.SettingsFilePath;
            _onSaved?.Invoke();
        }
        catch (System.Exception ex)
        {
            StatusMessage = "保存失败: " + ex.Message;
        }
    }
}
