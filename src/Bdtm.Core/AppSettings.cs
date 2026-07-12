using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bdtm.Core;

/// <summary>
/// Portable MVP settings persisted as settings.json under the app directory.
/// Property names match the original WinForms AppSettings where applicable.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Allow reading enums as strings if we add them later
        Converters = { new JsonStringEnumConverter() },
    };

    private string _settingsFile = string.Empty;
    private string[] _tagsFilesExt = { "txt", "caption" };

    public string Language { get; set; } = "zh-CN";
    public string SeparatorOnLoad { get; set; } = ",";
    public string SeparatorOnSave { get; set; } = ", ";
    public string DefaultTagsFileExtension { get; set; } = "txt";

    public string CaptionFileExtensions
    {
        get => string.Join(',', _tagsFilesExt);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _tagsFilesExt = new[] { "txt", "caption" };
                return;
            }

            _tagsFilesExt = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (_tagsFilesExt.Length == 0)
                _tagsFilesExt = new[] { "txt", "caption" };
        }
    }

    public bool FixTagsOnSaveLoad { get; set; } = true;
    public bool AskSaveChanges { get; set; } = true;
    public int PreviewSize { get; set; } = 130;
    public bool AutoSort { get; set; } = false;
    public string FfmpegPath { get; set; } = string.Empty;
    public string OnnxTaggerLastModelId { get; set; } = string.Empty;
    /// <summary>Optional absolute/relative root for ONNX models (contains org/repo folders).</summary>
    public string ModelsPath { get; set; } = string.Empty;
    public Wd14TaggerSettings Wd14Tagger { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();

    /// <summary>LLM model id used by character tag audit (empty = use Llm.VisionModel).</summary>
    public string CharacterTagAuditModel { get; set; } = string.Empty;

    /// <summary>Minimum character-tag frequency before audit considers the tag.</summary>
    public int CharacterTagAuditMinimumCount { get; set; } = 10;

    /// <summary>Sparse vs full character-tag audit style preference.</summary>
    public CharacterTagAuditStyle CharacterTagAuditStyle { get; set; } = CharacterTagAuditStyle.Sparse;

    /// <summary>Review-only vs summary-apply execution mode preference.</summary>
    public CharacterTagAuditExecutionMode CharacterTagAuditExecutionMode { get; set; } = CharacterTagAuditExecutionMode.Review;

    /// <summary>AiApiServer endpoint for background removal and other AI operations.</summary>
    public string AiApiEndpoint { get; set; } = "http://127.0.0.1:7866";

    /// <summary>Optional font preference without System.Drawing.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Optional font size preference without System.Drawing.</summary>
    public float? FontSize { get; set; }

    public AppSettings()
    {
    }

    /// <summary>
    /// Loads settings from appDir/settings.json. Creates defaults if missing or corrupt.
    /// Prefer <see cref="LoadUserSettings"/> on Linux desktop.
    /// </summary>
    public static AppSettings Load(string appDir)
    {
        if (string.IsNullOrWhiteSpace(appDir))
            throw new ArgumentException("Application directory is required.", nameof(appDir));

        Directory.CreateDirectory(appDir);
        return LoadFromFile(Path.Combine(appDir, "settings.json"));
    }

    /// <summary>Load from XDG config path (~/.config/bdtm/settings.json).</summary>
    public static AppSettings LoadUserSettings()
    {
        AppPaths.EnsureCreated();
        var settings = LoadFromFile(AppPaths.SettingsFilePath);
        if (string.IsNullOrWhiteSpace(settings.ModelsPath))
            settings.ModelsPath = AppPaths.DefaultModelsDir;
        return settings;
    }

    public static AppSettings LoadFromFile(string settingsFile)
    {
        if (string.IsNullOrWhiteSpace(settingsFile))
            throw new ArgumentException("Settings file path is required.", nameof(settingsFile));

        string? dir = Path.GetDirectoryName(settingsFile);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var settings = new AppSettings
        {
            _settingsFile = settingsFile,
        };

        if (!File.Exists(settings._settingsFile))
        {
            settings.Save();
            return settings;
        }

        try
        {
            string json = File.ReadAllText(settings._settingsFile);
            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null)
                throw new JsonException("Deserialized settings were null.");

            loaded._settingsFile = settings._settingsFile;
            loaded.Wd14Tagger ??= new Wd14TaggerSettings();
            loaded.Llm ??= new LlmSettings();
            if (string.IsNullOrWhiteSpace(loaded.Llm.Tag2NlSystemPrompt))
                loaded.Llm.Tag2NlSystemPrompt = LlmDefaults.Tag2NlSystemPrompt;
            loaded.Llm.Tag2NlConcurrency = Math.Clamp(loaded.Llm.Tag2NlConcurrency, 1, 100);
            loaded.FfmpegPath ??= string.Empty;
            loaded.OnnxTaggerLastModelId ??= string.Empty;
            loaded.ModelsPath ??= string.Empty;
            loaded.Language ??= "zh-CN";
            loaded.SeparatorOnLoad ??= ",";
            loaded.SeparatorOnSave ??= ", ";
            loaded.DefaultTagsFileExtension ??= "txt";
            if (loaded._tagsFilesExt is null || loaded._tagsFilesExt.Length == 0)
                loaded._tagsFilesExt = new[] { "txt", "caption" };
            loaded.CharacterTagAuditModel ??= string.Empty;
            if (loaded.CharacterTagAuditMinimumCount <= 0)
                loaded.CharacterTagAuditMinimumCount = 10;
            loaded.AiApiEndpoint ??= "http://127.0.0.1:7866";
            return loaded;
        }
        catch (Exception)
        {
            // Prefer recreate defaults like the original WinForms implementation.
            var defaults = new AppSettings
            {
                _settingsFile = settings._settingsFile,
            };
            defaults.Save();
            return defaults;
        }
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_settingsFile))
            throw new InvalidOperationException("Settings file path is not set. Use AppSettings.Load(appDir) first.");

        string? dir = Path.GetDirectoryName(_settingsFile);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(_settingsFile, json);
    }

    public string[] GetTagFilesExtensions() => (string[])_tagsFilesExt.Clone();
}

/// <summary>
/// Nested WD14 / ONNX tagger preferences (portable subset).
/// </summary>
public sealed class Wd14TaggerSettings
{
    public string SelectedModelRepo { get; set; } = "SmilingWolf/wd-eva02-large-tagger-v3";

    /// <summary>General tag threshold (legacy name: Threshold).</summary>
    public double Threshold { get; set; } = 0.52;

    /// <summary>Alias used by some call sites / docs; maps to Threshold.</summary>
    [JsonIgnore]
    public double GeneralThreshold
    {
        get => Threshold;
        set => Threshold = value;
    }

    public double CharacterThreshold { get; set; } = 0.85;
    public bool ReplaceUnderscoresWithSpaces { get; set; } = true;
    public string DownloadSource { get; set; } = "HfMirror";
    public Dictionary<string, Wd14ModelThresholds> ModelThresholds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class Wd14ModelThresholds
{
    public double Threshold { get; set; }
    public double CharacterThreshold { get; set; }
}

/// <summary>OpenAI-compatible vision tagging settings.</summary>
public sealed class LlmSettings
{
    private int _tag2NlConcurrency = 5;

    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string VisionModel { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 120;
    public string SystemPrompt { get; set; } =
        "You are an image tagger for anime/booru datasets. Reply with comma-separated English danbooru-style tags only. No explanations.";
    public string UserPrompt { get; set; } =
        "List relevant tags for this image as a comma-separated list.";
    public bool SplitTags { get; set; } = true;
    public string Splitter { get; set; } = ",";
    public float Temperature { get; set; } = 0.2f;

    public string Tag2NlSystemPrompt { get; set; } = LlmDefaults.Tag2NlSystemPrompt;

    public int Tag2NlConcurrency
    {
        get => _tag2NlConcurrency;
        set => _tag2NlConcurrency = Math.Clamp(value, 1, 100);
    }
}
