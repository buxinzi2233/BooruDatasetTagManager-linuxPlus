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

    /// <summary>Optional font preference without System.Drawing.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Optional font size preference without System.Drawing.</summary>
    public float? FontSize { get; set; }

    public AppSettings()
    {
    }

    /// <summary>
    /// Loads settings from appDir/settings.json. Creates defaults if missing or corrupt.
    /// </summary>
    public static AppSettings Load(string appDir)
    {
        if (string.IsNullOrWhiteSpace(appDir))
            throw new ArgumentException("Application directory is required.", nameof(appDir));

        Directory.CreateDirectory(appDir);

        var settings = new AppSettings
        {
            _settingsFile = Path.Combine(appDir, "settings.json"),
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
            loaded.FfmpegPath ??= string.Empty;
            loaded.OnnxTaggerLastModelId ??= string.Empty;
            loaded.ModelsPath ??= string.Empty;
            loaded.Language ??= "zh-CN";
            loaded.SeparatorOnLoad ??= ",";
            loaded.SeparatorOnSave ??= ", ";
            loaded.DefaultTagsFileExtension ??= "txt";
            if (loaded._tagsFilesExt is null || loaded._tagsFilesExt.Length == 0)
                loaded._tagsFilesExt = new[] { "txt", "caption" };
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
