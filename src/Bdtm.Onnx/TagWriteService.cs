using Bdtm.Core;

namespace Bdtm.Onnx;

public static class TagWriteService
{
    public static void ApplyTags(
        DatasetManager.DataItem item,
        IEnumerable<TagPrediction> tags,
        TagWriteMode mode,
        bool sortByConfidence = true)
    {
        if (item is null || tags is null)
            return;

        IEnumerable<TagPrediction> ordered = sortByConfidence
            ? tags.OrderByDescending(t => t.Confidence)
            : tags;

        List<string> names = ordered
            .Select(t => t.Tag?.Trim() ?? string.Empty)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Replace(' ', '_')) // common booru form when writing from WD14
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
            return;

        if (mode == TagWriteMode.ReplaceAll)
        {
            item.Tags.SetTags(names);
            return;
        }

        foreach (string name in names)
            item.Tags.Add(name, skipIfExists: true);
    }
}
