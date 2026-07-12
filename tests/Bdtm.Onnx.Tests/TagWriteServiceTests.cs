using Bdtm.Core;
using Bdtm.Onnx;
using Xunit;

namespace Bdtm.Onnx.Tests;

public class TagWriteServiceTests
{
    [Fact]
    public void AppendNew_AddsMissingTagsOnly()
    {
        var item = new DatasetManager.DataItem();
        item.Tags.Add("solo");

        TagWriteService.ApplyTags(
            item,
            new[]
            {
                new TagPrediction { Tag = "solo", Confidence = 0.9f },
                new TagPrediction { Tag = "1girl", Confidence = 0.8f },
            },
            TagWriteMode.AppendNew);

        Assert.Equal(2, item.Tags.Count);
        Assert.True(item.Tags.Contains("solo"));
        Assert.True(item.Tags.Contains("1girl"));
    }

    [Fact]
    public void ReplaceAll_OverwritesExistingTags()
    {
        var item = new DatasetManager.DataItem();
        item.Tags.Add("old");

        TagWriteService.ApplyTags(
            item,
            new[]
            {
                new TagPrediction { Tag = "new_a", Confidence = 0.5f },
                new TagPrediction { Tag = "new_b", Confidence = 0.9f },
            },
            TagWriteMode.ReplaceAll,
            sortByConfidence: true);

        Assert.Equal(2, item.Tags.Count);
        Assert.Equal("new_b", item.Tags[0].Tag); // higher confidence first
        Assert.Equal("new_a", item.Tags[1].Tag);
    }
}
