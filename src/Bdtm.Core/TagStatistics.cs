namespace Bdtm.Core;

public sealed class TagCountItem
{
    public string Tag { get; init; } = string.Empty;
    public int Count { get; init; }
    public string Chinese { get; init; } = string.Empty;
}

public static class TagStatistics
{
    public static IReadOnlyList<TagCountItem> Build(
        IEnumerable<DatasetManager.DataItem> items,
        ChineseTagLookup? lookup = null)
    {
        lookup ??= ChineseTagLookup.Empty;
        return items
            .SelectMany(i => i.Tags.Items.Select(t => t.Tag))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => t, StringComparer.Ordinal)
            .Select(g => new TagCountItem
            {
                Tag = g.Key,
                Count = g.Count(),
                Chinese = lookup.GetChinese(g.Key),
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
