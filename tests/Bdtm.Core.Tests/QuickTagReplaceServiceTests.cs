using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class QuickTagReplaceServiceTests
{
    // ── Same category ──────────────────────────────────────────────────────

    [Fact]
    public void GetReplacementSourceTags_ReturnsTagsWithSameCategory()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
            "red_sky",
            "green_grass",
            "white_cloud",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 10);

        // "blue_sky" category is "sky" -> matches "dark_sky", "red_sky"
        Assert.Contains("dark_sky", result);
        Assert.Contains("red_sky", result);
        // "green_grass" category is "grass" -> not included
        Assert.DoesNotContain("green_grass", result);
        // "white_cloud" category is "cloud" -> not included
        Assert.DoesNotContain("white_cloud", result);
    }

    [Fact]
    public void GetReplacementSourceTags_WithSpaceSeparator_RespectsCategory()
    {
        var tags = new List<string>
        {
            "action figure",
            "action scene",
            "blue_sky",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "action figure", threshold: 10);

        // Category of "action figure" is "figure" -> "action scene" (category "scene") does not match
        Assert.DoesNotContain("action scene", result);
        // "blue_sky" (category "sky") does not match either
        Assert.DoesNotContain("blue_sky", result);
        // No other tags share category "figure"
        Assert.Empty(result);
    }

    // ── Self exclusion ─────────────────────────────────────────────────────

    [Fact]
    public void GetReplacementSourceTags_ExcludesSelectedTag()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 10);

        Assert.DoesNotContain("blue_sky", result);
        // "dark_sky" is a valid candidate
        Assert.Single(result);
        Assert.Contains("dark_sky", result);
    }

    [Fact]
    public void GetReplacementSourceTags_ExcludesSelectedTag_CaseInsensitive()
    {
        var tags = new List<string>
        {
            "Blue_Sky",
            "Dark_Sky",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 10);

        Assert.DoesNotContain("blue_sky", result);
        Assert.DoesNotContain("Blue_Sky", result);
        Assert.Contains("dark_sky", result);
    }

    // ── Threshold ──────────────────────────────────────────────────────────

    [Fact]
    public void GetReplacementSourceTags_RespectsThreshold()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
            "dark_sky",
            "red_sky",
        };

        // threshold=2: only tags with count < 2
        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 2);

        // "dark_sky" appears twice => excluded
        Assert.DoesNotContain("dark_sky", result);
        // "red_sky" appears once => included
        Assert.Contains("red_sky", result);
    }

    [Fact]
    public void GetReplacementSourceTags_ThresholdOfOne_ReturnsOnlySingletons()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
            "dark_sky",
            "red_sky",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 1);

        // threshold=1 means count must be < 1, i.e. count == 0 — none qualify
        Assert.Empty(result);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────

    [Fact]
    public void GetReplacementSourceTags_NullTags_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QuickTagReplaceService.GetReplacementSourceTags(null!, "tag", 1));
    }

    [Fact]
    public void GetReplacementSourceTags_EmptyOrNullSelected_ReturnsEmpty()
    {
        var tags = new List<string> { "blue_sky", "dark_sky" };

        Assert.Empty(QuickTagReplaceService.GetReplacementSourceTags(tags, "", 10));
        Assert.Empty(QuickTagReplaceService.GetReplacementSourceTags(tags, "  ", 10));
        Assert.Empty(QuickTagReplaceService.GetReplacementSourceTags(tags, null!, 10));
    }

    [Fact]
    public void GetReplacementSourceTags_SelectedTagHasNoCategory_ReturnsEmpty()
    {
        var tags = new List<string> { "blue_sky", "dark_sky" };

        // A single-word tag like "sky": category = "sky", which is the tag itself.
        // That's fine — it matches "blue_sky" and "dark_sky" via EndsWith.
        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "sky", 10);
        Assert.Contains("blue_sky", result);
        Assert.Contains("dark_sky", result);
    }

    [Fact]
    public void GetReplacementSourceTags_NoMatchingCategory_ReturnsEmpty()
    {
        var tags = new List<string>
        {
            "green_grass",
            "white_cloud",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", 10);

        Assert.Empty(result);
    }

    [Fact]
    public void GetReplacementSourceTags_AllTagsBelowThreshold_ReturnsAllCandidates()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
            "red_sky",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 10);

        Assert.Contains("dark_sky", result);
        Assert.Contains("red_sky", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetReplacementSourceTags_UnderscoreCategory_Matching()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
            "red_grass",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 10);

        Assert.Contains("dark_sky", result);
        Assert.DoesNotContain("red_grass", result);
    }

    [Fact]
    public void GetReplacementSourceTags_ResultIsDistinct()
    {
        var tags = new List<string>
        {
            "blue_sky",
            "dark_sky",
            "dark_sky",
            "red_sky",
        };

        var result = QuickTagReplaceService.GetReplacementSourceTags(tags, "blue_sky", threshold: 10);

        // "dark_sky" appears twice but should be returned once
        Assert.Single(result, t => t == "dark_sky");
    }
}
