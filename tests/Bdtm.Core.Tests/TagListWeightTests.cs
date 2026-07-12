using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class TagListWeightTests
{
    [Fact]
    public void SetTags_WithWeights_PreservesValues()
    {
        var list = new TagList();
        list.SetTags(new[] { ("long_hair", 1.2f), ("1girl", 1f) });
        Assert.Equal(2, list.Count);
        Assert.Equal("long_hair", list[0].Tag);
        Assert.Equal(1.2f, list[0].Weight);
        Assert.Equal(1f, list[1].Weight);
        string formatted = list.Format(", ");
        Assert.Contains("long_hair", formatted);
    }
}
