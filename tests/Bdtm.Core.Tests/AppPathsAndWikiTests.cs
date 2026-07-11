using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class AppPathsAndWikiTests
{
    [Fact]
    public void AppPaths_DefaultModelsDir_EndsWithModels()
    {
        Assert.EndsWith("Models", AppPaths.DefaultModelsDir);
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.ConfigDir));
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.SettingsFilePath));
    }

    [Fact]
    public void DanbooruWiki_NormalizeAndUrl()
    {
        Assert.Equal("long_hair", DanbooruWikiClient.NormalizeTag(" Long Hair "));
        string url = DanbooruWikiClient.GetWikiUrl("long hair");
        Assert.Contains("danbooru.donmai.us/wiki_pages/", url);
        Assert.Contains("long_hair", url);
    }

    [Fact]
    public void LoadUserSettings_CreatesFileUnderConfig()
    {
        // Use temp via env override simulation: just LoadFromFile in temp
        string dir = Path.Combine(Path.GetTempPath(), "bdtm-cfg-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "settings.json");
        var s = AppSettings.LoadFromFile(file);
        s.Language = "en";
        s.ModelsPath = "/tmp/models-test";
        s.Save();
        Assert.True(File.Exists(file));
        var s2 = AppSettings.LoadFromFile(file);
        Assert.Equal("en", s2.Language);
        Assert.Equal("/tmp/models-test", s2.ModelsPath);
        try { Directory.Delete(dir, true); } catch { }
    }
}
