using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class TagListMoveTests
{
    [Fact]
    public void MoveUp_And_MoveDown_ReorderTags()
    {
        var list = new TagList();
        list.SetTags(new[] { ("a", 1f), ("b", 1f), ("c", 1f) });
        Assert.True(list.MoveDown(0));
        Assert.Equal(new[] { "b", "a", "c" }, list.Items.Select(i => i.Tag).ToArray());
        Assert.True(list.MoveUp(2));
        Assert.Equal(new[] { "b", "c", "a" }, list.Items.Select(i => i.Tag).ToArray());
        Assert.False(list.MoveUp(0));
        Assert.False(list.MoveDown(2));
    }
}
