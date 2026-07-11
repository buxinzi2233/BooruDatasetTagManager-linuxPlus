using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _tempRoot;

    public AppSettingsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "bdtm-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void Load_MissingFile_CreatesDefaults()
    {
        string settingsPath = Path.Combine(_tempRoot, "settings.json");
        Assert.False(File.Exists(settingsPath));

        var settings = AppSettings.Load(_tempRoot);

        Assert.True(File.Exists(settingsPath));
        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal(",", settings.SeparatorOnLoad);
        Assert.Equal(", ", settings.SeparatorOnSave);
        Assert.Equal("txt", settings.DefaultTagsFileExtension);
        Assert.True(settings.FixTagsOnSaveLoad);
        Assert.True(settings.AskSaveChanges);
        Assert.Equal(130, settings.PreviewSize);
        Assert.False(settings.AutoSort);
        Assert.Equal(string.Empty, settings.FfmpegPath);
        Assert.Equal(string.Empty, settings.OnnxTaggerLastModelId);
        Assert.NotNull(settings.Wd14Tagger);
        Assert.Equal(0.52, settings.Wd14Tagger.Threshold);
        Assert.Equal(0.85, settings.Wd14Tagger.CharacterThreshold);
        Assert.Equal("SmilingWolf/wd-eva02-large-tagger-v3", settings.Wd14Tagger.SelectedModelRepo);
        Assert.Contains("txt", settings.GetTagFilesExtensions());
        Assert.Contains("caption", settings.GetTagFilesExtensions());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsValues()
    {
        var settings = AppSettings.Load(_tempRoot);
        settings.Language = "en";
        settings.SeparatorOnLoad = ";";
        settings.SeparatorOnSave = " | ";
        settings.DefaultTagsFileExtension = "caption";
        settings.CaptionFileExtensions = "txt, tags, caption";
        settings.FixTagsOnSaveLoad = false;
        settings.AskSaveChanges = false;
        settings.PreviewSize = 200;
        settings.AutoSort = true;
        settings.FfmpegPath = "/usr/bin";
        settings.OnnxTaggerLastModelId = "model-1";
        settings.Wd14Tagger.Threshold = 0.4;
        settings.Wd14Tagger.CharacterThreshold = 0.9;
        settings.Wd14Tagger.SelectedModelRepo = "custom/repo";
        settings.Wd14Tagger.ReplaceUnderscoresWithSpaces = false;
        settings.Save();

        var reloaded = AppSettings.Load(_tempRoot);

        Assert.Equal("en", reloaded.Language);
        Assert.Equal(";", reloaded.SeparatorOnLoad);
        Assert.Equal(" | ", reloaded.SeparatorOnSave);
        Assert.Equal("caption", reloaded.DefaultTagsFileExtension);
        Assert.Equal("txt,tags,caption", reloaded.CaptionFileExtensions.Replace(" ", ""));
        Assert.False(reloaded.FixTagsOnSaveLoad);
        Assert.False(reloaded.AskSaveChanges);
        Assert.Equal(200, reloaded.PreviewSize);
        Assert.True(reloaded.AutoSort);
        Assert.Equal("/usr/bin", reloaded.FfmpegPath);
        Assert.Equal("model-1", reloaded.OnnxTaggerLastModelId);
        Assert.Equal(0.4, reloaded.Wd14Tagger.Threshold);
        Assert.Equal(0.9, reloaded.Wd14Tagger.CharacterThreshold);
        Assert.Equal("custom/repo", reloaded.Wd14Tagger.SelectedModelRepo);
        Assert.False(reloaded.Wd14Tagger.ReplaceUnderscoresWithSpaces);

        var exts = reloaded.GetTagFilesExtensions();
        Assert.Contains("txt", exts);
        Assert.Contains("tags", exts);
        Assert.Contains("caption", exts);
    }

    [Fact]
    public void Load_CorruptJson_RecreatesDefaults()
    {
        string settingsPath = Path.Combine(_tempRoot, "settings.json");
        File.WriteAllText(settingsPath, "{ this is not valid json !!!");

        var settings = AppSettings.Load(_tempRoot);

        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal(130, settings.PreviewSize);
        Assert.True(File.Exists(settingsPath));

        // File should be valid JSON after recovery
        string content = File.ReadAllText(settingsPath);
        Assert.Contains("zh-CN", content);
        Assert.DoesNotContain("this is not valid", content);
    }

    [Fact]
    public void CaptionFileExtensions_ParsesAndJoins()
    {
        var settings = AppSettings.Load(_tempRoot);
        settings.CaptionFileExtensions = " txt , caption , json ";

        string[] exts = settings.GetTagFilesExtensions();
        Assert.Equal(new[] { "txt", "caption", "json" }, exts);
        Assert.Equal("txt,caption,json", settings.CaptionFileExtensions);
    }
}
