using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class ChineseTagLookupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bdtm-zh-" + Guid.NewGuid().ToString("N"));

    public ChineseTagLookupTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void LoadFromFile_MapsEnglishToChinese()
    {
        string path = Path.Combine(_dir, "t.csv");
        File.WriteAllText(path, "long_hair,长发\n1girl,1个女性|一个女人\n");
        var lookup = ChineseTagLookup.LoadFromFile(path);
        Assert.Equal("长发", lookup.GetChinese("long_hair"));
        Assert.Equal("长发", lookup.GetChinese("long hair")); // fixTags spaces→underscore on load key only; query uses fixTags false
        // query normalizes lower only; ensure underscore form works
        Assert.Equal("1个女性", lookup.GetChinese("1girl"));
        Assert.Equal(string.Empty, lookup.GetChinese("unknown_tag"));
    }

    [Fact]
    public void TagStatistics_CountsAndChinese()
    {
        var dm = new DatasetManager();
        string root = Path.Combine(_dir, "ds");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "a.png"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(root, "b.png"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(root, "a.txt"), "1girl, long_hair");
        File.WriteAllText(Path.Combine(root, "b.txt"), "1girl, smile");
        Assert.True(dm.LoadFromFolder(root, new DatasetLoadOptions { FixTagsOnSaveLoad = false }));

        string csv = Path.Combine(_dir, "t.csv");
        File.WriteAllText(csv, "1girl,1个女性\nlong_hair,长发\n");
        var lookup = ChineseTagLookup.LoadFromFile(csv);
        var stats = TagStatistics.Build(dm.DataSet.Values, lookup);
        Assert.Contains(stats, s => s.Tag == "1girl" && s.Count == 2 && s.Chinese == "1个女性");
        Assert.Contains(stats, s => s.Tag == "long_hair" && s.Count == 1 && s.Chinese == "长发");
        Assert.Contains(stats, s => s.Tag == "smile" && s.Count == 1);
    }
}
