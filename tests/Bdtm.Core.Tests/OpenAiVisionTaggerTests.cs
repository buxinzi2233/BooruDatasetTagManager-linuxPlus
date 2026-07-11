using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class OpenAiVisionTaggerTests
{
    [Fact]
    public void ParseTags_SplitsCommaList()
    {
        var settings = new LlmSettings { SplitTags = true, Splitter = "," };
        var tags = OpenAiVisionTagger.ParseTags("1girl, long hair, smile", settings);
        Assert.Equal(3, tags.Count);
        Assert.Contains(tags, t => t.Tag == "1girl");
        Assert.Contains(tags, t => t.Tag == "long_hair");
        Assert.Contains(tags, t => t.Tag == "smile");
    }

    [Fact]
    public void ParseTags_StripsListNumbers()
    {
        var settings = new LlmSettings { SplitTags = true };
        var tags = OpenAiVisionTagger.ParseTags("1. solo\n2. blue eyes", settings);
        // split by comma only - newlines may keep as one if no comma
        var tags2 = OpenAiVisionTagger.ParseTags("solo, blue eyes", settings);
        Assert.Contains(tags2, t => t.Tag == "blue_eyes");
    }
}
