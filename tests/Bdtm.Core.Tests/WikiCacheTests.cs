using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class WikiCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bdtm-wiki-" + Guid.NewGuid().ToString("N"));

    public WikiCacheTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Set_And_TryGet_RoundTrip()
    {
        var cache = new WikiCache(_dir, TimeSpan.FromHours(1));
        var page = new DanbooruWikiPage
        {
            Title = "long_hair",
            Body = "hair longer than shoulders",
            OtherNames = new[] { "long hair" },
            Url = "https://danbooru.donmai.us/wiki_pages/long_hair",
        };
        cache.Set("Long Hair", page);
        var got = cache.TryGet("long_hair");
        Assert.NotNull(got);
        Assert.Equal("long_hair", got!.Title);
        Assert.Equal("hair longer than shoulders", got.Body);
    }

    [Fact]
    public void TryGet_Expired_ReturnsNull()
    {
        var cache = new WikiCache(_dir, TimeSpan.FromMilliseconds(1));
        cache.Set("solo", new DanbooruWikiPage { Title = "solo", Body = "x" });
        Thread.Sleep(20);
        Assert.Null(cache.TryGet("solo"));
    }

    [Fact]
    public void Invalidate_RemovesFile()
    {
        var cache = new WikiCache(_dir, TimeSpan.FromDays(1));
        cache.Set("1girl", new DanbooruWikiPage { Title = "1girl", Body = "y" });
        Assert.NotNull(cache.TryGet("1girl"));
        cache.Invalidate("1girl");
        Assert.Null(cache.TryGet("1girl"));
    }
}
