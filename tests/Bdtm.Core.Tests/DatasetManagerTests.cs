using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class DatasetManagerTests : IDisposable
{
    private readonly string _tempRoot;

    public DatasetManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "bdtm-dataset-" + Guid.NewGuid().ToString("N"));
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
            // best-effort
        }
    }

    private static void WriteImageStub(string path) => File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });

    private DatasetLoadOptions DefaultOptions() => new()
    {
        FixTagsOnSaveLoad = false,
        SeparatorOnLoad = ",",
        SeparatorOnSave = ", ",
        DefaultTagsFileExtension = "txt",
        TagFileExtensions = new[] { "txt", "caption" },
    };

    [Fact]
    public void LoadFromFolder_ReadsSidecarTags()
    {
        string img = Path.Combine(_tempRoot, "cat_01.png");
        string tags = Path.Combine(_tempRoot, "cat_01.txt");
        WriteImageStub(img);
        File.WriteAllText(tags, "1girl, solo, smile");

        var manager = new DatasetManager();
        Assert.True(manager.LoadFromFolder(_tempRoot, DefaultOptions()));
        Assert.Equal(1, manager.DataSet.Count);

        var item = manager.DataSet.Values.Single();
        Assert.Equal("cat_01", item.Name);
        Assert.Equal(3, item.Tags.Count);
        Assert.True(item.Tags.Contains("1girl"));
        Assert.True(item.Tags.Contains("solo"));
        Assert.True(item.Tags.Contains("smile"));
        Assert.False(item.IsModified(", "));
        Assert.False(manager.IsDataSetChanged());
    }

    [Fact]
    public void EditAndSave_WritesSidecarFile()
    {
        string img = Path.Combine(_tempRoot, "dog.png");
        WriteImageStub(img);
        // no sidecar yet

        var manager = new DatasetManager();
        Assert.True(manager.LoadFromFolder(_tempRoot, DefaultOptions()));
        var item = manager.DataSet.Values.Single();
        Assert.Equal(0, item.Tags.Count);

        item.Tags.Add("dog");
        item.Tags.Add("animal");
        Assert.True(item.IsModified(", "));
        Assert.True(manager.IsDataSetChanged());

        Assert.True(manager.SaveAll());
        Assert.False(item.IsModified(", "));
        Assert.False(manager.IsDataSetChanged());

        string textPath = Path.Combine(_tempRoot, "dog.txt");
        Assert.True(File.Exists(textPath));
        string content = File.ReadAllText(textPath);
        Assert.Contains("dog", content);
        Assert.Contains("animal", content);

        // reload
        var manager2 = new DatasetManager();
        Assert.True(manager2.LoadFromFolder(_tempRoot, DefaultOptions()));
        var reloaded = manager2.DataSet.Values.Single();
        Assert.Equal(2, reloaded.Tags.Count);
        Assert.True(reloaded.Tags.Contains("dog"));
        Assert.True(reloaded.Tags.Contains("animal"));
    }

    [Fact]
    public void AddTagToAll_And_DeleteTagFromAll()
    {
        WriteImageStub(Path.Combine(_tempRoot, "a.png"));
        WriteImageStub(Path.Combine(_tempRoot, "b.png"));
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "solo");
        File.WriteAllText(Path.Combine(_tempRoot, "b.txt"), "duo");

        var manager = new DatasetManager();
        Assert.True(manager.LoadFromFolder(_tempRoot, DefaultOptions()));
        Assert.Equal(2, manager.DataSet.Count);

        manager.AddTagToAll("masterpiece", skipExist: true);
        foreach (var item in manager.DataSet.Values)
            Assert.True(item.Tags.Contains("masterpiece"));

        manager.DeleteTagFromAll("masterpiece");
        foreach (var item in manager.DataSet.Values)
            Assert.False(item.Tags.Contains("masterpiece"));
    }

    [Fact]
    public void ReplaceTagInAll_UpdatesAllItems()
    {
        WriteImageStub(Path.Combine(_tempRoot, "x.png"));
        File.WriteAllText(Path.Combine(_tempRoot, "x.txt"), "old_tag, keep");

        var manager = new DatasetManager();
        Assert.True(manager.LoadFromFolder(_tempRoot, DefaultOptions()));
        manager.ReplaceTagInAll("old_tag", "new_tag");

        var item = manager.DataSet.Values.Single();
        Assert.False(item.Tags.Contains("old_tag"));
        Assert.True(item.Tags.Contains("new_tag"));
        Assert.True(item.Tags.Contains("keep"));
    }

    [Fact]
    public void LoadFromFolder_EmptyOrMissing_ReturnsFalse()
    {
        var manager = new DatasetManager();
        Assert.False(manager.LoadFromFolder(_tempRoot, DefaultOptions()));
        Assert.False(manager.LoadFromFolder(Path.Combine(_tempRoot, "nope"), DefaultOptions()));
    }

    [Fact]
    public void PromptParser_SimpleSplit_LowercasesAndTrims()
    {
        var items = PromptParser.ParsePrompt(" 1Girl, Solo , SMILE ", fixTagsForWeight: false, ",");
        Assert.Equal(new[] { "1girl", "solo", "smile" }, items.Select(i => i.Text).ToArray());
    }
}
